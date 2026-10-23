using System.Numerics;
using Celeste.Core.Components;

namespace Celeste.Mods.VortexHelper.Runtime
{
    internal sealed class CrushColorSwitchHistory : SourceComponentState
    {
        public Vector2 PreviousDirection;

        public CrushColorSwitchHistory() : base(false, false)
        {
        }
    }
}
