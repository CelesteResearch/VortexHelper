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
        public override void Render(ISourceRenderContext context)
        {
            this.puffer.Scale = this.scale * (1f + this.inflateWiggler.Value * 0.4f);
            this.puffer.FlipX = false;

            // gravity helper forces a scale change on the default Sprite
            if (Get<Sprite>() is { } sprite) sprite.Scale.Y = Math.Abs(sprite.Scale.Y);

            // invert sprites if required
            var inverted = runtime.IsActorInverted(this);
            if (inverted)
            {
                this.puffer.Y *= -1;
                this.pufferBowlTop.Y *= -1;
                this.pufferBowlBottom.Y *= -1;
                this.pufferBowlTop.Scale.Y *= -1;
                this.pufferBowlBottom.Scale.Y *= -1;
                this.puffer.Scale.Y *= -1;
            }

            Vector2 position = this.Position;
            var temporaryPosition = this.Position;
            temporaryPosition.Y -= 6.0f;
            this.Position = temporaryPosition;
            this.Position = position;

            base.Render(context);

            if (this.puffer.CurrentAnimationID is "alerted")
            {
                Vector2 pos = this.Position + new Vector2(3f, -4) * this.puffer.Scale;
                pos.X -= this.facing == Facings.Left ? 9 : 0;

                Vector2 to = this.lastPlayerPos + new Vector2(0f, -4f);
                Vector2 eyeOffset =
                    Vector2Geometry.AngleToVector(
                        Vector2Geometry.Angle(pos, to) + this.eyeSpin * ((float)Math.PI * 2f) * 2f, 1f);
                Vector2 eyePos = pos + new Vector2((float)Math.Round(eyeOffset.X) - 1,
                    (float)Math.Round(ClampedMap(eyeOffset.Y, -1f, 1f, -1f, 2f)) - 9);

                SourcePrimitiveDrawing.Point(context, this, eyePos, Color.Black);
            }

            if (this.fused && this.explodeTime != 0)
            {
                float r = this.explodeTimeLeft / this.explodeTime;
                for (float a = 0f; a < Math.PI * 2 * r; a += 0.06f)
                {
                    Vector2 p = this.Center + new Vector2((float)Math.Sin(a), -(float)Math.Cos(a)) * 16 -
                                Vector2.UnitY * 5 - Vector2.UnitX;
                    SourcePrimitiveDrawing.Point(context, this, p,
                        Color.Lerp(Color.OrangeRed, Color.LawnGreen, a / (float)Math.PI));
                }
            }

            if (inverted)
            {
                this.puffer.Y *= -1;
                this.pufferBowlTop.Y *= -1;
                this.pufferBowlBottom.Y *= -1;
                this.pufferBowlTop.Scale.Y *= -1;
                this.pufferBowlBottom.Scale.Y *= -1;
                this.puffer.Scale.Y *= -1;
            }
        }
    }
}
