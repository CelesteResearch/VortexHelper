using System;
using System.Numerics;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Display;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Mechanisms.SwitchGates;
using Celeste.Game.Gameplay.Platforms.CrushBlocks;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Geometry.Tiling;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.VortexHelper.Runtime.Particles;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Scene.Components.Oscillation;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Actors.Debris;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;
using Celeste.Simulation.Presentation.Lighting;

namespace Celeste.Mods.VortexHelper.Entities.Blocks
{
    [CustomEntity("VortexHelper/BubbleWrapBlock")]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.BubbleWrapBlock")]
    [SceneTracked(false)]
    public partial class BubbleWrapBlock : SourceSolidEntityState
    {
        private enum States
        {
            Idle,
            Gone
        }

        private States state;
        private readonly IOriginalSwitchGateProcess process;
        private readonly string actorRegistryKey;
        private readonly string autoFallingBlockRegistryKey;
        private readonly bool canDash;
        private readonly float respawnTime;
        private float timer;
        private float rectEffectInflate;
        private readonly SoundSourceComponentState breakSfx;
        private readonly AtlasEntry[,,] nineSlice;
        private Vector2 wobbleScale = Vector2.One;
        private readonly WigglerComponent wobble;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        public static BubbleWrapBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data)
            => new((IOriginalSwitchGateProcess)level.EngineContext, data, offset, level.SolidWorldFactory);

        public BubbleWrapBlock(IOriginalSwitchGateProcess process, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : this(process, data.Position + offset, data.Width, data.Height, data.Bool("canDash"),
                data.Float("respawnTime"), worlds)
        {
        }

        public BubbleWrapBlock(IOriginalSwitchGateProcess process, Vector2 position, int width, int height,
            bool canDash, float respawnTime, ISourceSolidWorldFactory worlds)
            : base(position, width, height, true, worlds.Create(position, true))
        {
            this.process = process;
            var catalog = ((ISourceSceneCatalogEnvironment)process).SceneCatalog;
            actorRegistryKey = catalog.Find(typeof(SourceActorEntityState)).RegistryKey;
            autoFallingBlockRegistryKey = catalog.Find(typeof(AutoFallingBlock)).RegistryKey;
            SurfaceSoundIndex = 8;
            this.canDash = canDash;
            this.respawnTime = respawnTime;
            var block = process.GameGraphics.GameAtlas["objects/VortexHelper/bubbleWrapBlock/bubbleBlock"];
            var outline = process.GameGraphics.GameAtlas["objects/VortexHelper/bubbleWrapBlock/bubbleOutline"];
            nineSlice = new AtlasEntry[3, 3, 2];
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                nineSlice[i, j, 0] = block.GetSubtexture(i * 8, j * 8, 8, 8);
                nineSlice[i, j, 1] = outline.GetSubtexture(i * 8, j * 8, 8, 8);
            }

            Add(new LightOccludeState());
            Add(breakSfx = process.SoundSources.Create());
            Add(wobble = process.WigglerPool.Create(.75f, 4.2f,
                v => wobbleScale = new Vector2(1f + v * .12f, 1f - v * .12f)));
            OnDashCollide = Dashed;
        }

        public DashCollisionResult Dashed(RoomPlayerSession player, Vector2 direction)
        {
            if (!process.SaveContext.CurrentSave.Assists.Invincible && player.CollideCheck("Celeste.Spikes"))
                return DashCollisionResult.NormalCollision;
            if (!canDash) return DashCollisionResult.NormalCollision;
            if (state == States.Gone) return DashCollisionResult.Ignore;
            if (player.Machine.State == 5) player.Machine.State = 0;
            Break();
            return DashCollisionResult.Rebound;
        }

        public void Break()
        {
            breakSfx.Play("event:/game/general/wall_break_stone");
            for (int i = 0; i < Width / 8f; i++)
            for (int j = 0; j < Height / 8f; j++)
            {
                var debris = new DebrisState(process, SourceWorld.TargetMotionPolicy)
                    .Init(Position + new Vector2(4 + i * 8, 4 + j * 8), '1').BlastFrom(Center);
                debris.SetTexture(process.GameGraphics.GameAtlas["debris/VortexHelper/BubbleWrapBlock"]);
                ((ISourceScene)Scene).SourceScene.QueueSourceEntity(debris);
            }

            Level.Shake(.1f);
            DisableStaticMovers();
            state = States.Gone;
            Collidable = false;
            timer = respawnTime;
        }

        public override void Update()
        {
            base.Update();
            if (timer > 0f) timer -= process.DeltaTime;
            if (timer <= 0f && CheckEntitySafe()) Respawn();
            if (state == States.Gone)
                rectEffectInflate = ScalarMath.Approach(rectEffectInflate, 3f, 20f * process.DeltaTime);
        }

        private bool CheckEntitySafe()
        {
            var entities = ((ISourceScene)Scene).SourceScene.Entities;
            foreach (var entity in entities.GetTrackedEntities("Celeste.Solid"))
                if (entity is OriginalCrushBlockState or MoveBlockState &&
                    SourceEntityCollision.Check(this, (ISourceEntityCollider)entity))
                    return false;
            return !CollideCheck(actorRegistryKey) && !CollideCheck("Celeste.FallingBlock") &&
                   (!entities.IsTracked(autoFallingBlockRegistryKey) || !CollideCheck(autoFallingBlockRegistryKey));
        }

        private void Respawn()
        {
            if (Collidable) return;
            wobble.Start();
            RespawnParticles();
            rectEffectInflate = 0f;
            EnableStaticMovers();
            breakSfx.Play("event:/game/05_mirror_temple/redbooster_reappear");
            Collidable = true;
            state = States.Idle;
        }

        private void RespawnParticles()
        {
            var particle = BubbleWrapParticles.For(process).Respawn;
            for (int i = 0; i < Width; i += 4)
            {
                Level.Particles.Emit(particle,
                    new Vector2(X + 2f + i + SourceRandomMath.Range(process.Random, -1, 1), Y), -(float)Math.PI / 2f);
                Level.Particles.Emit(particle,
                    new Vector2(X + 2f + i + SourceRandomMath.Range(process.Random, -1, 1), Bottom - 1f),
                    (float)Math.PI / 2f);
            }

            for (int j = 0; j < Height; j += 4)
            {
                Level.Particles.Emit(particle,
                    new Vector2(X, Y + 2f + j + SourceRandomMath.Range(process.Random, -1, 1)), (float)Math.PI);
                Level.Particles.Emit(particle,
                    new Vector2(Right - 1f, Y + 2f + j + SourceRandomMath.Range(process.Random, -1, 1)), 0f);
            }
        }
    }
}
