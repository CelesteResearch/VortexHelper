using System.Numerics;
using Celeste.Core.Components.Animation;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Environment.Core;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;

namespace Celeste.Mods.VortexHelper.Entities.Hazards.Bumpers
{
    [CustomEntity("VortexHelper/VortexCustomBumper")]
    [Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.VortexBumper")]
    public partial class VortexBumper : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly Sprite sprite;
        private readonly Sprite spriteEvil;
        private readonly VertexLightState light;
        private readonly BloomPointState bloom;
        private readonly ParticleTypePresentation ambience;
        private readonly ParticleTypePresentation launch;
        private readonly WigglerComponent hitWiggler;
        private Vector2 hitDir;
        private readonly SineWaveState sine;
        private Vector2 anchor;
        private bool goBack;
        private float respawnTimer;
        private bool fireMode;
        private readonly bool twoDashes;
        private readonly bool oneUse;
        private bool deadly;
        private readonly bool notCoreMode;
        private readonly bool wobble;

        public VortexBumper(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine, new VortexBumperInitialization(engine, data, offset))
        {
        }

        private VortexBumper(ISourceEngineContext engine, VortexBumperInitialization initialization)
            : base(((Celeste.Core.Components.ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                initialization.Position)
        {
            this.engine = engine;
            Collider = new CircleColliderGeometry(((ISourceVectorEnvironment)engine).VectorArithmetic, 12f);
            Add(new PlayerColliderComponentState(OnPlayer));
            Add(sine = new SineWaveState(engine, .44f).Randomize());
            wobble = initialization.Wobble;
            sprite = initialization.CreateSprite();
            twoDashes = initialization.TwoDashes;
            oneUse = !twoDashes;
            ambience = initialization.Ambience;
            launch = initialization.Launch;
            notCoreMode = initialization.NotCoreMode;
            Add(sprite);
            Add(spriteEvil = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.SpriteFactory.Create("bumper_evil"));
            spriteEvil.Visible = false;
            Add(light = new VertexLightState(engine,
                twoDashes ? new PackedColor(0, 100, 0, 255) : new PackedColor(255, 140, 0, 255), 1, 16, 32));
            Add(bloom = new BloomPointState(.5f, 16f));
            anchor = Position;
            if (initialization.Node.HasValue)
            {
                Vector2 start = Position, end = initialization.Node.Value;
                var tween = SourceTweenState.Create(engine.TweenPool, SourceTweenState.TweenMode.Looping,
                    ((ISourceEasingEnvironment)engine).EasingArithmetic.CubeInOut, 1.81818187f, start: true);
                tween.OnUpdate = t =>
                    anchor = goBack
                        ? Vector2Interpolation.Lerp(end, start, t.Eased,
                            ((ISourceScalarEnvironment)engine).ScalarArithmetic)
                        : Vector2Interpolation.Lerp(start, end, t.Eased,
                            ((ISourceScalarEnvironment)engine).ScalarArithmetic);
                tween.OnComplete = _ => goBack = !goBack;
                Add(tween);
            }

            UpdatePosition();
            Add(hitWiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(1.2f, 2f,
                _ => spriteEvil.Position = hitDir * hitWiggler.Value * 8f));
            if (!notCoreMode) Add(new CoreModeListenerState(OnChangeMode));
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            if (notCoreMode) return;
            fireMode = ((OriginalLevelSceneRoot)Scene).CoreMode == RoomCoreMode.Hot;
            spriteEvil.Visible = fireMode;
            sprite.Visible = !fireMode;
        }

        private void OnChangeMode(RoomCoreMode coreMode)
        {
            fireMode = coreMode == RoomCoreMode.Hot;
            if (!fireMode && deadly) return;
            spriteEvil.Visible = fireMode;
            sprite.Visible = !fireMode;
        }
    }
}
