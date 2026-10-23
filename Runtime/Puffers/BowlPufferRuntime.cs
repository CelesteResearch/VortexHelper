using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Interop;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Hazards.Puffer;
using Celeste.Game.Gameplay.Interaction.Springs;
using Celeste.Game.Gameplay.Platforms.CrushBlocks;
using Celeste.Graphics.Content;
using Celeste.Mods.VortexHelper.Entities.Holdables.Puffers;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;
using Celeste.Simulation.Presentation.Particles.Definitions;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.VortexHelper.Runtime.Puffers
{
    public sealed class BowlPufferRuntime : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, BowlPufferRuntime> Runtimes = new();

        private static readonly Action<OriginalPufferState> PufferExplode =
            (Action<OriginalPufferState>)typeof(OriginalPufferState)
                .GetMethod("Explode", BindingFlags.Instance | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Action<OriginalPufferState>));

        private static readonly Action<OriginalPufferState> PufferGotoGone =
            (Action<OriginalPufferState>)typeof(OriginalPufferState)
                .GetMethod("GotoGone", BindingFlags.Instance | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Action<OriginalPufferState>));

        private static readonly Func<OriginalCrushBlockState, RoomPlayerSession, Vector2, DashCollisionResult>
            CrushOnDashed =
                (Func<OriginalCrushBlockState, RoomPlayerSession, Vector2, DashCollisionResult>)
                typeof(OriginalCrushBlockState)
                    .GetMethod("OnDashed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .CreateDelegate(
                        typeof(Func<OriginalCrushBlockState, RoomPlayerSession, Vector2, DashCollisionResult>));

        private readonly ISourceEngineContext engine;
        private readonly BowlPufferGravityImports gravity = new();
        private IDisposable gravityLease;
        private Hook springHook;
        private bool disposed;

        public SpriteBank Sprites { get; private set; }
        public ParticleTypePresentation Shatter { get; private set; }
        public ParticleTypePresentation Crystal { get; private set; }

        private delegate void OriginalSpringConstructor(OriginalSpringState spring, Vector2 position,
            SpringOrientation orientation, bool playerCanUse, IOriginalSpringProcess process);

        private delegate void SpringConstructorHook(OriginalSpringConstructor original, OriginalSpringState spring,
            Vector2 position, SpringOrientation orientation, bool playerCanUse, IOriginalSpringProcess process);

        private BowlPufferRuntime(ISourceEngineContext engine)
        {
            this.engine = engine;
            try
            {
                gravityLease = ((ISourceModInteropEnvironment)engine).ModInterop.RegisterOwned(
                    new SourceModInteropActualTypeBinding(typeof(BowlPufferGravityImports), gravity,
                        exportedMethods: Array.Empty<MethodInfo>()));
                springHook = new Hook(typeof(OriginalSpringState).GetConstructor(new[]
                        { typeof(Vector2), typeof(SpringOrientation), typeof(bool), typeof(IOriginalSpringProcess) }),
                    (SpringConstructorHook)OnSpringConstructed);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static BowlPufferRuntime Load(ISourceEngineContext engine)
        {
            if (Runtimes.TryGetValue(engine, out var existing)) return existing;
            var runtime = new BowlPufferRuntime(engine);
            Runtimes.Add(engine, runtime);
            return runtime;
        }

        public static BowlPufferRuntime For(ISourceEngineContext engine) =>
            Runtimes.TryGetValue(engine, out var runtime)
                ? runtime
                : throw new InvalidOperationException("BowlPuffer runtime has not been loaded for this engine.");

        public static void LoadContent(ISourceEngineContext engine)
        {
            var runtime = For(engine);
            var xml = new XmlDocument();
            using (var stream = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry
                   .Get("VortexHelper:/Graphics/BowlPufferSprites").Stream) xml.Load(stream);
            var sprites = new SpriteBank(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas, xml)
                { XMLPath = "Graphics/BowlPufferSprites.xml" };
            var particles = ((IOriginalGameplayProcess)engine).ParticleTypes;
            var shatter = new ParticleTypePresentation(particles.Refill.P_Shatter)
            {
                Color = PackedColor.White,
                Color2 = PackedColor.LightBlue,
                ColorMode = ParticleTypePresentation.ColorModes.Blink,
                SpeedMax = 75f,
                SpeedMin = 0f
            };
            var crystal = new ParticleTypePresentation(particles.Seeker.P_Regen)
            {
                SpeedMax = 40f,
                Acceleration = Vector2.UnitY * 30
            };
            runtime.Sprites = sprites;
            runtime.Shatter = shatter;
            runtime.Crystal = crystal;
        }

        public static void Unload(ISourceEngineContext engine)
        {
            if (Runtimes.TryGetValue(engine, out var runtime)) runtime.Dispose();
        }

        public bool IsActorInverted(SourceActorEntityState actor) => gravity.IsActorInverted?.Invoke(actor) ?? false;
        public void ExplodePuffer(OriginalPufferState puffer) => PufferExplode(puffer);
        public void GotoGonePuffer(OriginalPufferState puffer) => PufferGotoGone(puffer);

        public void DashCrushBlock(OriginalCrushBlockState block, Vector2 direction) =>
            CrushOnDashed(block, null, direction);

        private void OnSpringConstructed(OriginalSpringConstructor original, OriginalSpringState spring,
            Vector2 position, SpringOrientation orientation, bool playerCanUse, IOriginalSpringProcess process)
        {
            original(spring, position, orientation, playerCanUse, process);
            if (ReferenceEquals(process, engine)) spring.Add(new BowlPuffer.BowlPufferCollider(OnBowlPuffer));
        }

        private static void OnBowlPuffer(BowlPuffer puffer, OriginalSpringState spring)
        {
            puffer.HitSpring(spring);
            spring.AnimateSpringBounce();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                springHook?.Dispose();
            }
            finally
            {
                gravityLease?.Dispose();
                if (Runtimes.TryGetValue(engine, out var current) && ReferenceEquals(current, this))
                    Runtimes.Remove(engine);
            }
        }
    }
}
