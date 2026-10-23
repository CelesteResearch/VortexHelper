using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Entities.Lightning;
using Celeste.Game.Entities.Mechanisms.RumbleWalls;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.VortexHelper.Triggers
{
    [CustomEntity("VortexHelper/KillLightningTrigger")]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Triggers.KillLightningTrigger")]
    public sealed class KillLightningTrigger : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly bool permanent;

        public static SceneEntityState Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new KillLightningTrigger(level.EngineContext, data, offset);

        public KillLightningTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
            data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            permanent = EntityAttributeReader.Bool(data.Values, "permanent");
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (permanent && ((OriginalLevelSceneRoot)Scene).Session.GetFlag("disable_lightning")) RemoveSelf();
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            var process = (IOriginalGameplayProcess)engine;
            process.AudioSession.Play("event:/vortexHelperEvents/game/killLightningTrigger/break", Center,
                process.SaveContext);
            Break();
            ((IOriginalGameInputProcessEnvironment)engine).InputSource.Rumble(RumbleStrength.Strong, RumbleLength.Long);
        }

        private void Break()
        {
            var level = (OriginalLevelSceneRoot)Scene;
            if (permanent) level.Session.SetFlag("disable_lightning", true);
            OriginalRumbleTriggerState.ManuallyTrigger(engine, Center.X, 1.2f);
            Tags = ((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value;
            OwnedComponents.Add(new SimulationCoroutine(engine,
                OriginalLightningSequence.RemoveRoutine(level, RemoveSelf)));
        }
    }
}
