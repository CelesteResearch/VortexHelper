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
        public static BowlPuffer Create(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(data, offset, level.EngineContext);

        private Player FirstPlayer
        {
            get
            {
                var entities = ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(typeof(Player).FullName);
                return entities.Count == 0 ? null : (Player)entities[0];
            }
        }

        private List<object> Tracked(string key) => ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(key);
        private bool CollideCheck(ISourceEntityCollider other) => SourceEntityCollision.Check(this, other);

        private List<T> CollideAll<T>() where T : class, ISourceEntityCollider
        {
            var results = new List<T>();
            var entities = ((ISourceScene)Scene).SourceScene.Entities.Entities;
            for (int i = 0; i < entities.Count; i++)
                if (entities[i] is T target && SourceEntityCollision.Check(this, target))
                    results.Add(target);
            return results;
        }

        private float Approach(float value, float target, float amount) => ScalarMath.Approach(value, target, amount,
            ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);

        private Vector2 Approach(Vector2 value, Vector2 target, float amount) =>
            ((ISourceVectorEnvironment)engine).VectorArithmetic.Approach(value, target, amount);

        private static float ClampedMap(float value, float min, float max, float newMin = 0f, float newMax = 1f) =>
            ScalarMath.ClampedMap(value, min, max, newMin, newMax);

        private void PlaySound(string path, Vector2 position, string parameter = null, float value = 0f)
        {
            var process = (IOriginalGameplayProcess)engine;
            if (parameter == null) process.AudioSession.Play(path, position, process.SaveContext);
            else process.AudioSession.Play(path, position, process.SaveContext, parameter, value);
        }
    }
}
