using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Core.Display;
using Celeste.Mods.VortexHelper.Configuration;
using Celeste.Mods.VortexHelper.Modules;

namespace Celeste.Mods.VortexHelper.Runtime
{
    public sealed class VortexColorRuntime
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, VortexColorRuntime> owners = new();
        public readonly ISourceEngineContext Engine;
        private readonly VortexHelperModule module;

        private VortexColorRuntime(VortexHelperModule module)
        {
            this.module = module;
            Engine = module.Engine;
        }

        public static void Register(VortexHelperModule module) =>
            owners.Add(module.Engine, new VortexColorRuntime(module));

        public static void Unregister(ISourceEngineContext engine) => owners.Remove(engine);

        public static VortexColorRuntime For(ISourceEngineContext engine) => owners.GetValue(engine,
            _ => throw new System.InvalidOperationException("VortexHelper color runtime is not loaded."));

        public VortexHelperSession Session => (VortexHelperSession)module._Session;
        public bool IsActive(SwitchBlockColor color) => color == Session.SessionSwitchBlockColor;

        public static SwitchBlockColor FromIndex(int index) => index switch
        {
            1 => SwitchBlockColor.Rose, 2 => SwitchBlockColor.Orange, 3 => SwitchBlockColor.Lime,
            _ => SwitchBlockColor.Blue
        };

        public static PackedColor GetColor(SwitchBlockColor color) => color switch
        {
            SwitchBlockColor.Rose => new(255, 50, 101), SwitchBlockColor.Orange => new(255, 149, 50),
            SwitchBlockColor.Lime => new(156, 255, 50), _ => new(50, 50, 255)
        };

        public static int GetSoundParam(SwitchBlockColor color) => color switch
        {
            SwitchBlockColor.Rose => 1, SwitchBlockColor.Orange => 2, SwitchBlockColor.Lime => 3, _ => 0
        };
    }
}
