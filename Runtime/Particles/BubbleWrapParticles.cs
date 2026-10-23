using System.Runtime.CompilerServices;
using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Graphics.Display.Colors;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.VortexHelper.Runtime.Particles
{
    public sealed class BubbleWrapParticles
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, BubbleWrapParticles> owned = new();
        public ParticleTypePresentation Respawn { get; private set; }
        public static BubbleWrapParticles For(ISourceEngineContext engine) => owned.GetValue(engine, _ => new());

        public static void Initialize(ISourceEngineContext engine)
        {
            For(engine).Respawn = new ParticleTypePresentation
            {
                Color = SourceColorInterpolation.Lerp(PackedColor.Purple, PackedColor.White, .2f,
                    ((ISourceScalarEnvironment)engine).ScalarArithmetic),
                FadeMode = ParticleTypePresentation.FadeModes.Late,
                SpeedMin = 20f, SpeedMax = 50f, SpeedMultiplier = .1f, Size = 1f,
                LifeMin = .4f, LifeMax = .8f, DirectionRange = (float)System.Math.PI / 6f
            };
        }
    }
}
