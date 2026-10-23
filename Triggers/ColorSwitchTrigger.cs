using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.VortexHelper.Configuration;
using Celeste.Mods.VortexHelper.Runtime;
using Celeste.Mods.VortexHelper.Entities.Blocks;
using Celeste.Mods.VortexHelper.Entities.Switches;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.VortexHelper.Triggers
{
    [CustomEntity("VortexHelper/ColorSwitchTrigger")]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Triggers.ColorSwitchTrigger")]
    public sealed class ColorSwitchTrigger : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly bool oneUse, silent;
        private readonly SwitchBlockColor color;

        public static SceneEntityState Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new ColorSwitchTrigger(level.EngineContext, data, offset);

        public ColorSwitchTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
            data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            oneUse = EntityAttributeReader.Bool(data.Values, "oneUse");
            silent = EntityAttributeReader.Bool(data.Values, "silent");
            color = VortexColorRuntime.FromIndex(EntityAttributeReader.Int(data.Values, "index"));
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            var runtime = VortexColorRuntime.For(engine);
            runtime.Session.SessionSwitchBlockColor = color;
            ColorSwitch.UpdateColorSwitches(Scene, color);
            if (SwitchBlock.RoomHasSwitchBlock(Scene, color) && !silent) ColorSwitch.PlaySwitchSound(engine);
            if (oneUse) RemoveSelf();
        }
    }
}
