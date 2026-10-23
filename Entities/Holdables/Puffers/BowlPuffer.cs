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
    [CustomEntity("VortexHelper/BowlPuffer = Create")]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.BowlPuffer")]
    public partial class BowlPuffer : SourceActorEntityState, ISourceEngineContextEntity
    {
        private readonly ISourceEngineContext engine;
        private readonly BowlPufferRuntime runtime;
        private readonly string waterKey, solidKey, templeGateKey, barrierKey, touchSwitchKey, debrisKey;
        private ParticleType P_Shatter => runtime.Shatter;
        private ParticleType P_Crystal => runtime.Crystal;
        public ISourceEngineContext EngineContext => engine;

        private enum States
        {
            Gone,
            Crystal
        }

        private States state = States.Crystal;
        private Facings facing = Facings.Right;

        private readonly bool noRespawn;

        // Uses three sprites so that we can play the vanilla puffer animations without extra sprite work.
        private readonly Sprite pufferBowlBottom, puffer, pufferBowlTop;

        private readonly Vector2 startPosition;

        private SimpleCurve returnCurve;
        private float goneTimer;
        private float eyeSpin;

        private Vector2 previousPosition;
        private Vector2 Speed;
        private Vector2 prevLiftSpeed;

        private float noGravityTimer;
        private float cantExplodeTimer;
        private float shatterTimer;

        private readonly float explodeTime;
        private float explodeTimeLeft;
        private bool fused = false;

        private Vector2 lastPlayerPos;

        private readonly Wiggler inflateWiggler;
        private Vector2 scale;

        private bool exploded = false;

        private readonly Circle pushRadius;

        private Level Level;

        private readonly Holdable Hold;
        private readonly Collision onCollideH, onCollideV;
        private HoldableCollider hitSeeker;

        private float hardVerticalHitSoundCooldown;
        private float swatTimer;

        private const float ChainExplosionDelay = 0.1f;
        private float chainTimer = ChainExplosionDelay;
        private bool chainExplode = false;

        // TODO: Custom sound.

        public BowlPuffer(EntityData data, Vector2 offset, ISourceEngineContext engine)
            : this(data.Position + offset + Vector2.UnitY * 8f, data.Bool("noRespawn"), data.Float("explodeTimer", 0f),
                engine)
        {
        }

        public BowlPuffer(Vector2 position, bool noRespawn, float explodeTimer, ISourceEngineContext engine)
            : base(new PixelActor(null, position), engine,
                ((ISourcePlatformTargetMotionEnvironment)engine).TargetMotionPolicy)
        {
            this.engine = engine;
            this.runtime = BowlPufferRuntime.For(engine);
            var catalog = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog;
            waterKey = catalog.Find(typeof(Water)).RegistryKey;
            solidKey = catalog.Find(typeof(Solid)).RegistryKey;
            templeGateKey = catalog.Find(typeof(TempleGate)).RegistryKey;
            barrierKey = catalog.Find(typeof(PufferBarrier)).RegistryKey;
            touchSwitchKey = catalog.Find(typeof(TouchSwitch)).RegistryKey;
            debrisKey = catalog.Find(typeof(FloatingDebris)).RegistryKey;
            this.noRespawn = noRespawn;
            this.explodeTime = explodeTimer;
            this.previousPosition = position;
            this.startPosition = position;
            this.Depth = 100;
            this.Collider = new Hitbox(8f, 10f, -4f, -10f);

            Add(this.pufferBowlBottom = runtime.Sprites.Create("pufferBowlBottom"));
            Add(this.puffer = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.SpriteFactory.Create("pufferFish"));
            Add(this.pufferBowlTop = runtime.Sprites.Create("pufferBowlTop"));
            this.puffer.Y -= 9f;

            // Weird offset needed
            var bowlOffset = new Vector2(-32, -43);
            this.pufferBowlTop.Position = this.pufferBowlBottom.Position += bowlOffset;

            this.pushRadius = new Circle(((ISourceVectorEnvironment)engine).VectorArithmetic, 40f);

            this.inflateWiggler =
                ((Celeste.Scene.Components.Oscillation.ISourceWigglerPoolScope)engine).WigglerPool.Create(0.6f, 2f);
            Add(this.inflateWiggler);

            Add(this.Hold = new Holdable(engine, 0.1f)
            {
                PickupCollider = new Hitbox(21f, 17f, -11f, -17f),
                SlowFall = false,
                SlowRun = true,
                OnPickup = OnPickup,
                OnRelease = OnRelease,
                DangerousCheck = value => Dangerous((HoldableCollider)value),
                OnHitSeeker = value => HitSeeker((Seeker)value),
                OnSwat = (value, direction) => Swat((HoldableCollider)value, direction),
                OnHitSpring = value => HitSpring((Spring)value),
                OnHitSpinner = value => HitSpinner((Entity)value),
                SpeedGetter = () => this.Speed,
                SpeedSetter = (speed) => this.Speed = speed,
            });

            this.onCollideH = OnCollideH;
            this.onCollideV = OnCollideV;

            this.scale = Vector2.One;

            this.LiftSpeedGraceTime = 0.1f;
            AddTag(((IOriginalGameTagsEnvironment)engine).Tags.TransitionUpdate.Value);
            Add(new MirrorReflection());
        }

        private void AllSpritesPlay(string anim)
        {
            this.pufferBowlBottom.Play(anim);
            this.puffer.Play(anim);
            this.pufferBowlTop.Play(anim);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            this.Level = (Level)Scene;
        }
    }
}
