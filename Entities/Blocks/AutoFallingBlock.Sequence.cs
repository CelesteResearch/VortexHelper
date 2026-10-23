using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Collections;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Audio.Surfaces;
using Celeste.Game.Content.Maps;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Tiles;
using Celeste.Graphics.Content.Tiles.Autotiling;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Tiles;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Core.Randomness;
using Celeste.Mods.VortexHelper.Entities.Switches;

namespace Celeste.Mods.VortexHelper.Entities.Blocks
{
    public partial class AutoFallingBlock
    {
        private IEnumerator Sequence()
        {
            while (true)
            {
                for (int i = 2; i < this.Width; i += 4)
                {
                    if (SourceSceneCollision.CheckPoint(Registry, "Celeste.Solid", this.TopLeft + new Vector2(i, -2f)))
                        Level.Particles.Emit(Process.ParticleTypes.FallingBlock.P_FallDustA, 2,
                            new Vector2(this.X + i, this.Y), Vector2.One * 4f, (float)Math.PI / 2f);
                    Level.Particles.Emit(Process.ParticleTypes.FallingBlock.P_FallDustB, 2,
                        new Vector2(this.X + i, this.Y), Vector2.One * 4f);
                }

                float speed = 0f;
                float maxSpeed = 160f;

                while (true)
                {
                    OriginalLevelSceneRoot level = Level;

                    speed = ScalarMath.Approach(speed, maxSpeed, 500f * engine.Frame.DeltaTime);
                    if (MoveVCollideSolids(speed * engine.Frame.DeltaTime, thruDashBlocks: true))
                        break;

                    this.Safe = false;
                    if (this.Top > level.Bounds.Bottom + 16 || (this.Top > level.Bounds.Bottom - 1 &&
                                                                CollideCheck("Celeste.Solid",
                                                                    this.Position + new Vector2(0f, 1f))))
                    {
                        this.Collidable = this.Visible = false;
                        yield return 0.2f;

                        if (((OriginalChapterMapData)level.Session.MapData).CanTransitionTo(level,
                                new Vector2(this.Center.X, this.Bottom + 12f)))
                        {
                            yield return 0.2f;
                            Level.Shake();
                            ((IOriginalGameInputProcessEnvironment)engine).InputSource.Rumble(RumbleStrength.Strong,
                                RumbleLength.Medium);
                        }

                        RemoveSelf();
                        DestroyStaticMovers();
                        yield break;
                    }

                    yield return null;
                }

                this.Safe = true;

                if (this.Y != this.originalY)
                {
                    ImpactSfx();
                    ((IOriginalGameInputProcessEnvironment)engine).InputSource.Rumble(RumbleStrength.Strong,
                        RumbleLength.Medium);
                    Level.DirectionalShake(Vector2.UnitY, 0.3f);
                    StartShaking();
                    LandParticles();

                    foreach (BubbleWrapBlock e in Registry.GetTrackedEntities(typeof(BubbleWrapBlock)))
                        if (SourceEntityCollision.CheckAt(this, e, this.Position + Vector2.UnitY))
                            e.Break();

                    foreach (ColorSwitch e in Registry.GetTrackedEntities(typeof(ColorSwitch)))
                        if (SourceEntityCollision.CheckAt(this, e, this.Position + Vector2.UnitY))
                            e.Switch(Vector2.UnitY);

                    yield return 0.2f;
                    StopShaking();
                }

                if (CollideCheck("Celeste.SolidTiles", this.Position + new Vector2(0f, 1f)))
                    break;

                while (CollideCheck("Celeste.Platform", this.Position + new Vector2(0f, 1f)))
                    yield return 0.1f;
            }

            this.Safe = true;
        }
    }
}
