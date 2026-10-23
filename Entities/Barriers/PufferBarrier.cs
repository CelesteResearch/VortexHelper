using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Barriers;

namespace Celeste.Mods.VortexHelper.Entities.Barriers
{
    [CustomEntity("VortexHelper/PufferBarrier = Load")]
    [SceneTracked]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.PufferBarrier")]
    public class PufferBarrier : SourceSolidEntityState
    {
        private float solidifyDelay;
        public static readonly PackedColor ParticleColor = PackedColor.Lerp(PackedColor.Orange, PackedColor.White, .5f);
        public float Flash;
        public bool Flashing;
        private readonly List<Vector2> particles = new();
        private readonly float[] speeds = { 12f, 20f, 40f };
        private readonly List<PufferBarrier> adjacent = new();
        private readonly ISourceEngineContext engine;

        public static PufferBarrier Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(data, offset, level.SolidWorldFactory);

        public PufferBarrier(EntityData data, Vector2 offset, ISourceSolidWorldFactory worlds)
            : this(data.Position + offset, data.Width, data.Height, worlds)
        {
        }

        public PufferBarrier(Vector2 position, float width, float height, ISourceSolidWorldFactory worlds)
            : base(position, width, height, false, worlds.Create(position, false))
        {
            engine = SourceWorld.EngineContext;
            Collidable = false;
            BarrierParticleMotion.FillUp(new ParticleMotionState(this));
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntity<PufferBarrierRenderer>().Track(this);
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntity<PufferBarrierRenderer>().Untrack(this);
        }

        public override void Update()
        {
            if (Flashing)
            {
                Flash = ScalarMath.Approach(Flash, 0f, engine.DeltaTime * 4f,
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
                if (Flash <= 0f) Flashing = false;
            }
            else if (solidifyDelay > 0f) solidifyDelay -= engine.DeltaTime;

            BarrierParticleMotion.AdvanceUp(new ParticleMotionState(this));
            base.Update();
        }

        public void OnTouchPuffer()
        {
            Flash = 1f;
            solidifyDelay = 1f;
            Flashing = true;
            SourceSceneCollision.IntoRect(((ISourceScene)Scene).SourceScene.Entities,
                ((ISourceScene)Scene).SourceScene.Catalog.Find(typeof(PufferBarrier)).Name,
                new IntRect((int)X, (int)Y - 2, (int)Width, (int)Height + 4), adjacent);
            SourceSceneCollision.IntoRect(((ISourceScene)Scene).SourceScene.Entities,
                ((ISourceScene)Scene).SourceScene.Catalog.Find(typeof(PufferBarrier)).Name,
                new IntRect((int)X - 2, (int)Y, (int)Width + 4, (int)Height), adjacent);
            foreach (PufferBarrier barrier in adjacent)
                if (!barrier.Flashing)
                    barrier.OnTouchPuffer();
            adjacent.Clear();
        }

        public override void Render(ISourceRenderContext context)
        {
            Vector2 offset = Vector2.UnitY * (Height - 1f);
            foreach (Vector2 particle in particles)
                SourcePrimitiveDrawing.Point(context, this, Position + particle + offset, ParticleColor);
            if (Flashing)
                SourcePrimitiveDrawing.Rect(context, this, Collider.AbsoluteLeft, Collider.AbsoluteTop, Width, Height,
                    PackedColor.White * Flash * .5f);
        }

        private readonly struct ParticleMotionState : IBarrierParticleMotionState
        {
            private readonly PufferBarrier owner;
            public ParticleMotionState(PufferBarrier owner) => this.owner = owner;
            public List<Vector2> Particles => owner.particles;
            public float[] Speeds => owner.speeds;
            public float Width => owner.Width;
            public float Height => owner.Height;
            public float DeltaTime => owner.engine.DeltaTime;
            public ISourceRandom Random => ((ISourceRandomEnvironment)owner.engine).Random;
        }
    }
}
