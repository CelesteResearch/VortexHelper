using Facings = Celeste.Simulation.Gameplay.Actors.Facing.FacingDirection;
using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Audio;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Controls.Source;
using Celeste.Game.Gameplay.Effects.Slashes;
using Celeste.Game.Gameplay.Mechanisms.DashSwitches;
using Celeste.Game.Gameplay.Enemies.Seekers;
using Celeste.Game.Entities.Platforms.SwapBlocks;
using Celeste.Graphics.Animation;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using Celeste.Simulation.Gameplay.Walls.Concealment;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Effects.Mirrors;
using Celeste.Simulation.Presentation.Player.Death;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using Holdable = Celeste.Simulation.Gameplay.Holdables.HoldableState;
using HoldableCollider = Celeste.Simulation.Gameplay.Holdables.HoldableColliderState;
using Collision = Celeste.Physics.Collision.SourceCollisionHandler;
using CollisionData = Celeste.Physics.Collision.PixelCollision;
using EntityID = Celeste.Core.Maps.MapEntityId;
using VertexLight = Celeste.Simulation.Presentation.Lighting.VertexLightState;
using MirrorReflection = Celeste.Simulation.Presentation.Effects.Mirrors.MirrorReflectionState;
using Entity = Celeste.Scene.Entities.SceneEntityState;
using Player = Celeste.Simulation.RoomPlayerSession;
using Level = Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Seeker = Celeste.Game.Gameplay.Enemies.Seekers.OriginalSeekerState;
using SeekerBarrier = Celeste.Simulation.Gameplay.Enemies.Seeker.SeekerBarrierState;
using DashSwitch = Celeste.Game.Gameplay.Mechanisms.DashSwitches.OriginalDashSwitchState;
using SwapBlock = Celeste.Game.Entities.Platforms.SwapBlocks.OriginalSwapBlockState;
using Solid = Celeste.Simulation.Gameplay.Platforms.Foundation.SourceSolidEntityState;
using Celeste.Mods.VortexHelper.Runtime.Puffers;
using Celeste.Mods.VortexHelper.Entities.Barriers;
using Celeste.Mods.VortexHelper.Entities.Switches;
using Celeste.Core.Rendering;
using Celeste.Scene.Components.Oscillation;
using Celeste.Core.Mathematics.Easing;
using Spring = Celeste.Game.Gameplay.Interaction.Springs.OriginalSpringState;
using Circle = Celeste.Physics.Collision.Shapes.CircleColliderGeometry;
using Puffer = Celeste.Game.Gameplay.Hazards.Puffer.OriginalPufferState;
using TheoCrystal = Celeste.Game.Gameplay.Holdables.TheoCrystal.TheoCrystalState;
using TouchSwitch = Celeste.Game.Gameplay.Mechanisms.Switches.OriginalTouchSwitchState;
using FloatingDebris = Celeste.Game.Gameplay.Environment.Debris.OriginalFloatingDebrisState;
using TempleCrackedBlock = Celeste.Game.Gameplay.Walls.Breakable.OriginalTempleCrackedBlockState;
using DashBlock = Celeste.Game.Gameplay.Walls.Breakable.OriginalDashBlockState;
using FallingBlock = Celeste.Game.Gameplay.Platforms.FallingBlocks.OriginalFallingBlockState;
using MoveBlock = Celeste.Game.Gameplay.Platforms.MoveBlocks.MoveBlockState;
using CrushBlock = Celeste.Game.Gameplay.Platforms.CrushBlocks.OriginalCrushBlockState;
using LightningBreakerBox = Celeste.Game.Gameplay.Hazards.Lightning.LightningBreakerBoxState;
using TempleGate = Celeste.Game.Gameplay.Doors.TempleGates.OriginalTempleGateState;
using Water = Celeste.Game.Gameplay.Environment.Water.OriginalWaterState;
using EntityData = Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData;
using System.Linq;
using System.Collections.Generic;
using Celeste.Core.Randomness;
using Celeste.Graphics.Content;
using Celeste.Graphics.Rendering;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Mods.VortexHelper.Entities.Blocks;
using Actor = Celeste.Simulation.Gameplay.Actors.Foundation.SourceActorEntityState;
using Wiggler = Celeste.Scene.Components.Oscillation.WigglerComponent;

namespace Celeste.Mods.VortexHelper.Entities.Holdables.Puffers
{
    public partial class BowlPuffer
    {
        #region Explosion

        private void Explode(bool playsound = true)
        {
            this.Collider = this.pushRadius;
            if (playsound)
                PlaySound("event:/new_content/game/10_farewell/puffer_splode", this.Position);

            this.puffer.Play("explode");
            if (this.state == States.Crystal)
                ShatterBowl();

            this.exploded = true;

            // Yeah, there's a lot going in there.
            DoEntityCustomInteraction();

            this.exploded = false;
            this.Collider = null;

            Level level = (Level)Scene;
            level.Shake();
            Celeste.Game.Presentation.Effects.Explosions.PufferExplosionPresentation.Emit(level, engine, this);
        }

        private void DoEntityCustomInteraction()
        {
            Player player = FirstPlayer;

            // Touch Switches
            foreach (TouchSwitch e in Tracked(touchSwitchKey))
                if (CollideCheck(e))
                    e.TurnOn();

            // Floating Debris
            foreach (FloatingDebris e in Tracked(debrisKey))
                if (CollideCheck(e))
                    e.OnExplode(this.Position);

            foreach (Actor e in CollideAll<Actor>())
            {
                switch (e)
                {
                    case Player p:
                        p.ExplodeLaunch(this.Position, snapUp: false, sidesOnly: true);
                        break;

                    case TheoCrystal crystal:
                        if (SourceSceneCollision.FirstLine<object>(((ISourceScene)Scene).SourceScene.Entities,
                                "Celeste.Solid", this.Position, crystal.Center) == null)
                            crystal.ExplodeLaunch(this.Position);
                        break;

                    case Puffer puffer:
                        runtime.ExplodePuffer(puffer);
                        runtime.GotoGonePuffer(puffer);
                        break;

                    case BowlPuffer puffer:
                        puffer.chainExplode = !puffer.exploded && puffer.state != States.Gone;
                        break;
                }
            }

            foreach (Solid e in CollideAll<Solid>())
            {
                switch (e)
                {
                    case TempleCrackedBlock block:
                        block.Break(this.Position);
                        break;

                    case ColorSwitch colorSwitch:
                        colorSwitch.Switch(Vector2Geometry.FourWayNormal(e.Center - this.Center));
                        break;

                    case DashBlock block:
                        block.Break(this.Center, Vector2Geometry.FourWayNormal(e.Center - this.Center), true, true);
                        break;

                    case FallingBlock block:
                        block.Triggered = true;
                        break;

                    case MoveBlock block:
                        block.OnStaticMoverTrigger(null);
                        break;

                    case CrushBlock block:
                        runtime.DashCrushBlock(block, Vector2Geometry.FourWayNormal(block.Center - this.Center));
                        break;

                    case BubbleWrapBlock block:
                        block.Break();
                        break;

                    case LightningBreakerBox box:
                        if (player is not null)
                        {
                            float stamina = player.Data.Stamina;
                            int dashes = player.Data.Dashes;
                            box.OnDashCollide(player, Vector2Geometry.FourWayNormal(box.Center - this.Center));
                            player.Data.Dashes = dashes;
                            player.Data.Stamina = stamina;
                        }

                        break;
                }

                ;
            }
        }

        #endregion

        private void SetBowlVisible(bool visible) =>
            this.pufferBowlBottom.Visible = this.pufferBowlTop.Visible = visible;

        #region State gotos

        private void GotoGone()
        {
            SetBowlVisible(false);

            Vector2 control = this.Position + (this.startPosition - this.Position) * 0.5f;

            if (((ISourceVectorEnvironment)engine).VectorArithmetic.LengthSquared(this.startPosition - this.Position) >
                100f)
            {
                if (Math.Abs(this.Position.Y - this.startPosition.Y) > Math.Abs(this.Position.X - this.startPosition.X))
                    control.X += this.Position.X > this.startPosition.X ? -24 : 24;
                else
                    control.Y += this.Position.Y > this.startPosition.Y ? -24f : 24;
            }

            this.returnCurve = new SimpleCurve(this.Position, this.startPosition, control);
            this.goneTimer = 2.5f;

            this.state = States.Gone;

            this.Collidable = false;
            this.Collider = null;
        }

        private void GotoCrystal()
        {
            SetBowlVisible(true);

            this.fused = false;

            Add(this.Hold);
            this.Collider = new Hitbox(8f, 10f, -4f, -10f);

            this.facing = Facings.Right;

            if (this.state is States.Gone)
            {
                this.Position = this.startPosition;
                AllSpritesPlay("recover");
                PlaySound("event:/new_content/game/10_farewell/puffer_reform", this.Position);
            }

            this.Speed = this.prevLiftSpeed = Vector2.Zero;

            if (!CollideCheck(solidKey, this.Position + Vector2.UnitY))
                this.noGravityTimer = 0.25f;

            this.state = States.Crystal;
        }

        #endregion

        private bool CollidePufferBarrierCheck()
        {
            foreach (PufferBarrier barrier in Tracked(barrierKey))
            {
                barrier.Collidable = true;
                if (CollideCheck(barrier))
                {
                    barrier.OnTouchPuffer();
                    barrier.Collidable = false;
                    return true;
                }

                barrier.Collidable = false;
            }

            return false;
        }

        private void PlayerThrowSelf(Player player)
        {
            if (player?.Holding?.Holding?.Entity == this)
                player.Throw();
        }

        private void ShatterBowl()
        {
            ((IOriginalGameInputProcessEnvironment)engine).InputSource.Rumble(RumbleStrength.Strong,
                RumbleLength.Short);
            Level level = (Level)Scene;
            level.Shake(0.175f);

            for (int i = 0; i < 10; i++)
                level.ParticlesFG.Emit(P_Shatter, 1, this.Center, Vector2.One * 7f,
                    SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random) * (float)Math.PI * 2);
            for (float t = 0f; t < (float)Math.PI * 2f; t += 0.17453292f)
                level.Particles.Emit(P_Crystal, this.Center + Vector2.UnitY * -6, t);
        }
    }
}
