using System;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using MonoMod.ModInterop;

namespace Celeste.Mods.VortexHelper.Runtime.Puffers
{
    [ModImportName("GravityHelper")]
    public sealed class BowlPufferGravityImports
    {
        public Func<SourceActorEntityState, bool> IsActorInverted;
    }
}
