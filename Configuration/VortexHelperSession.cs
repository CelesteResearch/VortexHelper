using Celeste.Everest.Modules.Persistence;

namespace Celeste.Mods.VortexHelper.Configuration
{
    public sealed partial class VortexHelperSession : EverestModuleSession
    {
        public SwitchBlockColor SessionSwitchBlockColor { get; set; } = SwitchBlockColor.Blue;
    }
}
