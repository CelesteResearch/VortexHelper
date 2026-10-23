using System;
using System.Numerics;
using MonoMod.ModInterop;

namespace Celeste.Mods.VortexHelper.Runtime.Boosters
{
    [ModImportName("GravityHelper")]
    public sealed class PurpleBoosterGravityImports
    {
        public Func<bool> IsPlayerInverted;
        public Action BeginOverride;
        public Action EndOverride;
        public bool Inverted => IsPlayerInverted?.Invoke() ?? false;
        public Vector2 InvertIfRequired(Vector2 vector) => Inverted ? new Vector2(vector.X, -vector.Y) : vector;
    }
}
