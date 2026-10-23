using Celeste.Core.Components;
using Celeste.Mods.VortexHelper.Entities.Boosters;

namespace Celeste.Mods.VortexHelper.Runtime.Boosters
{
    public sealed class FloorBoosterPlayerState : SourceComponentState
    {
        public float Speed;
        public FloorBooster Last;

        public FloorBoosterPlayerState() : base(false, false)
        {
        }
    }
}
