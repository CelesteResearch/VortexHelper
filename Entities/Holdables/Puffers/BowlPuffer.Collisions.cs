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
        private void OnCollideH(CollisionData data)
        {
            if (this.state is not States.Crystal)
                return;

            if (data.Hit is DashSwitch dashSwitch)
                dashSwitch.OnDashCollide(null, Vector2.UnitX * Math.Sign(this.Speed.X));

            PlaySound("event:/game/05_mirror_temple/crystaltheo_hit_side", this.Position);

            if (Math.Abs(this.Speed.X) > 100f)
                ImpactParticles(data.Direction);

            this.Speed.X *= -0.4f;
        }

        private void OnCollideV(CollisionData data)
        {
            if (this.state is not States.Crystal)
                return;

            if (data.Hit is DashSwitch dashSwitch)
                dashSwitch.OnDashCollide(null, Vector2.UnitY * Math.Sign(this.Speed.Y));

            if (this.Speed.Y > 0f)
            {
                if (this.hardVerticalHitSoundCooldown <= 0f)
                {
                    PlaySound("event:/game/05_mirror_temple/crystaltheo_hit_ground", this.Position, "crystal_velocity",
                        ClampedMap(this.Speed.Y, 0f, 200f));
                    this.hardVerticalHitSoundCooldown = 0.5f;
                }
                else
                    PlaySound("event:/game/05_mirror_temple/crystaltheo_hit_ground", this.Position, "crystal_velocity",
                        0f);
            }

            if (this.Speed.Y > 160f)
                ImpactParticles(data.Direction);

#pragma warning disable CS9336 // 原发布实现保留此模式的实际行为。
            if (this.Speed.Y > 140f && data.Hit is not SwapBlock or DashSwitch)
#pragma warning restore CS9336
                this.Speed.Y *= -0.6f;
            else
                this.Speed.Y = 0f;
        }

        private void ImpactParticles(Vector2 dir)
        {
            float direction;
            Vector2 position, positionRange;

            if (dir.X > 0f)
            {
                direction = (float)Math.PI;
                position = new Vector2(this.Right, this.Y - 4f);
                positionRange = Vector2.UnitY * 6f;
            }
            else if (dir.X < 0f)
            {
                direction = 0f;
                position = new Vector2(this.Left, this.Y - 4f);
                positionRange = Vector2.UnitY * 6f;
            }
            else if (dir.Y > 0f)
            {
                direction = -(float)Math.PI / 2f;
                position = new Vector2(this.X, this.Bottom);
                positionRange = Vector2.UnitX * 6f;
            }
            else
            {
                direction = (float)Math.PI / 2f;
                position = new Vector2(this.X, this.Top);
                positionRange = Vector2.UnitX * 6f;
            }

            this.Level.Particles.Emit(((IOriginalGameplayProcess)engine).ParticleTypes.TheoCrystal.P_Impact, 12,
                position, positionRange, direction);
        }

        public void Swat(HoldableCollider hc, int dir)
        {
            if (!this.Hold.IsHeld || this.hitSeeker is not null)
                return;

            this.swatTimer = 0.1f;
            this.hitSeeker = hc;
            this.Hold.Holder.Swat(dir);
        }

        public bool Dangerous(HoldableCollider hc)
            => !this.Hold.IsHeld && this.Speed != Vector2.Zero && this.hitSeeker != hc;

        protected override void OnSquish(CollisionData data)
        {
            if (TrySquishWiggle(data) || this.state is States.Gone)
                return;

            Explode();
            GotoGone();
        }

        public override bool IsRiding(ISourceEntityCollider solid)
            => this.Speed.Y == 0f && base.IsRiding(solid);

        public void HitSeeker(Seeker seeker)
        {
            if (!this.Hold.IsHeld)
                this.Speed =
                    ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(this.Center - seeker.Center,
                        120f);
            PlaySound("event:/game/05_mirror_temple/crystaltheo_hit_side", this.Position);
        }

        #region Hitting other entities

        public void HitSpinner(Entity spinner)
        {
            if (!this.Hold.IsHeld && ((ISourceVectorEnvironment)engine).VectorArithmetic.Length(this.Speed) < 0.01f &&
                ((ISourceVectorEnvironment)engine).VectorArithmetic.Length(this.LiftSpeed) < 0.01f &&
                ((ISourceVectorEnvironment)engine).VectorArithmetic.Length(this.previousPosition - this.ExactPosition) <
                0.01f && OnGround())
            {
                int num = Math.Sign(this.X - spinner.X);
                if (num == 0) num = 1;

                this.Speed.X = num * 120f;
                this.Speed.Y = -30f;
            }
        }

        public bool HitSpring(Spring spring)
        {
            if (this.Hold.IsHeld)
                return false;

            if (spring.Orientation == SpringOrientation.Floor && this.Speed.Y >= 0f)
            {
                this.Speed.X *= 0.5f;
                this.Speed.Y = -160f;
                this.noGravityTimer = 0.15f;
                return true;
            }

            if (spring.Orientation == SpringOrientation.WallLeft && this.Speed.X <= 0f)
            {
                MoveTowardsY(spring.CenterY + 5f, 4f);
                this.Speed.X = 220f;
                this.Speed.Y = -80f;
                this.noGravityTimer = 0.1f;
                return true;
            }

            if (spring.Orientation == SpringOrientation.WallRight && this.Speed.X >= 0f)
            {
                MoveTowardsY(spring.CenterY + 5f, 4f);
                this.Speed.X = -220f;
                this.Speed.Y = -80f;
                this.noGravityTimer = 0.1f;
                return true;
            }

            return false;
        }

        #endregion

        #region Picking up & releasing

        private void OnPickup()
        {
            this.Speed = Vector2.Zero;
            AddTag(((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value);
        }

        private void OnRelease(Vector2 force)
        {
            RemoveTag(((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value);

            if (force.X != 0f && force.Y == 0f)
                force.Y = -0.4f;

            this.Speed = force * 200f;
            if (this.Speed != Vector2.Zero)
                this.noGravityTimer = 0.1f;
        }

        #endregion
    }
}
