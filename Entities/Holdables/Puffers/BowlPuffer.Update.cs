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
        public override void Update()
        {
            base.Update();

            this.eyeSpin = Approach(this.eyeSpin, 0f, engine.DeltaTime * 1.5f);
            this.scale = Approach(this.scale, Vector2.One, engine.DeltaTime);

            if (this.state != States.Gone && this.cantExplodeTimer > 0f)
                this.cantExplodeTimer -= engine.DeltaTime;

            if (this.shatterTimer > 0f)
                this.shatterTimer -= 1.5f * engine.DeltaTime;

            Player player = this.FirstPlayer;
            if (player is not null)
                this.lastPlayerPos = player.Center;

            switch (this.state)
            {
                default:
                    break;

                case States.Crystal:
                {
                    if (this.swatTimer > 0f)
                        this.swatTimer -= engine.DeltaTime;

                    this.hardVerticalHitSoundCooldown -= engine.DeltaTime;
                    this.Depth = 100;

                    if (this.fused)
                    {
                        if (CollideCheck(waterKey))
                        {
                            this.fused = false;
                            PlaySound("event:/new_content/game/10_farewell/puffer_shrink", this.Position);
                            this.puffer.Play("unalert");
                        }
                    }
                    else
                    {
                        if (CollidePufferBarrierCheck())
                        {
                            this.fused = true;
                            this.explodeTimeLeft = this.explodeTime;
                            PlaySound("event:/new_content/game/10_farewell/puffer_expand", this.Position);
                            this.puffer.Play("alert");
                        }
                    }

                    if (this.fused)
                    {
                        if (this.explodeTimeLeft > 0f)
                            this.explodeTimeLeft -= engine.DeltaTime;

                        if (this.explodeTimeLeft <= 0f)
                        {
                            GotoGone();
                            ShatterBowl();
                            Explode();
                            PlayerThrowSelf(player);
                            return;
                        }
                    }

                    if (this.chainExplode)
                    {
                        if (this.chainTimer > 0f)
                        {
                            this.chainTimer -= engine.DeltaTime;
                        }

                        if (this.chainTimer <= 0f)
                        {
                            this.chainTimer = ChainExplosionDelay;
                            this.chainExplode = false;
                            GotoGone();
                            ShatterBowl();
                            Explode();
                            PlayerThrowSelf(player);
                            return;
                        }
                    }

                    if (this.Hold.IsHeld)
                    {
                        this.prevLiftSpeed = Vector2.Zero;
                        this.noGravityTimer = 0f;
                    }
                    else
                    {
                        bool inWater = CollideCheck(waterKey, this.Position + Vector2.UnitY * -8);

                        if (OnGround())
                        {
                            CarryableActorMotionSteps.AdvanceGroundSpeed(this, engine, ref Speed, ref prevLiftSpeed,
                                ref noGravityTimer);
                            if (inWater) Speed.Y = Approach(Speed.Y, -30f, 800f * engine.DeltaTime * 0.8f);
                        }
                        else if (Hold.ShouldHaveGravity)
                            CarryableActorMotionSteps.AdvanceAirSpeed(engine, ref Speed, ref noGravityTimer,
                                inWater ? -30f : 200f, inWater ? 0.7f : 1f);

                        this.previousPosition = this.ExactPosition;

                        MoveH(this.Speed.X * engine.DeltaTime, this.onCollideH);
                        MoveV(this.Speed.Y * engine.DeltaTime, this.onCollideV);

                        if (!this.Level.Transitioning)
                        {
                            if (this.Center.X > this.Level.Bounds.Right)
                            {
                                MoveH(32f * engine.DeltaTime);
                                if (this.Left - 8f > this.Level.Bounds.Right)
                                    RemoveSelf();
                            }
                            else if (this.Left < this.Level.Bounds.Left)
                            {
                                this.Left = this.Level.Bounds.Left;
                                this.Speed.X *= -0.4f;
                            }
                            else if (this.Top < this.Level.Bounds.Top - 4)
                            {
                                this.Top = this.Level.Bounds.Top + 4;
                                this.Speed.Y = 0f;
                            }
                            else if (this.Top > this.Level.Bounds.Bottom)
                            {
                                MoveV(-5);
                                Explode();
                                GotoGone();
                            }

                            if (this.X < this.Level.Bounds.Left + 10)
                                MoveH(32f * engine.DeltaTime);
                        }

                        if (player is not null)
                        {
                            TempleGate templeGate = CollideFirst<TempleGate>(templeGateKey);
                            if (templeGate is not null)
                            {
                                templeGate.Collidable = false;
                                MoveH(Math.Sign(player.X - this.X) * 32 * engine.DeltaTime);
                                templeGate.Collidable = true;
                            }
                        }
                    }

                    this.Hold.CheckAgainstColliders();
                    if (this.hitSeeker is not null && this.swatTimer <= 0f && !this.hitSeeker.Check(this.Hold))
                        this.hitSeeker = null;
                    break;
                }

                case States.Gone:
                {
                    float prev = this.goneTimer;
                    this.goneTimer -= engine.DeltaTime;

                    if (this.goneTimer <= 0.5f)
                    {
                        if (prev > 0.5f && this.returnCurve.GetLengthParametric(8,
                                ((ISourceCurveEnvironment)engine).CurveArithmetic,
                                ((ISourceVectorEnvironment)engine).VectorArithmetic) > 8f)
                            PlaySound("event:/new_content/game/10_farewell/puffer_return", this.Position);
                        this.Position = this.returnCurve.GetPoint(
                            ((ISourceEasingEnvironment)engine).EasingArithmetic.CubeInOut(ClampedMap(this.goneTimer,
                                0.5f, 0f)), ((ISourceCurveEnvironment)engine).CurveArithmetic);
                    }

                    if (this.goneTimer <= 2.1f && prev > 2.1f && this.noRespawn)
                        RemoveSelf();

                    if (this.goneTimer <= 0f)
                    {
                        this.Visible = this.Collidable = true;
                        GotoCrystal();
                    }

                    break;
                }
            }
        }
    }
}
