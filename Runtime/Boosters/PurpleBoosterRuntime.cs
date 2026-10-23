using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Reflection.Interop;
using Celeste.Core.Scheduling;
using Celeste.Everest.Runtime.Events.Player;
using Celeste.Everest.Modules.Runtime;
using Celeste.Mods.VortexHelper.Configuration;
using Celeste.Mods.VortexHelper.Entities.Boosters;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Contacts;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.VortexHelper.Runtime.Boosters
{
    public sealed partial class PurpleBoosterRuntime : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, PurpleBoosterRuntime> owners = new();
        public static PurpleBoosterRuntime For(ISourceEngineContext engine) => owners.GetValue(engine, e => new(e));
        public ISourceEngineContext Engine { get; }
        public PurpleBoosterGravityImports Gravity { get; } = new();
        public PurpleBoosterResources Resources { get; }
        private SourceEverestModule module;
        public VortexHelperSession Session => (VortexHelperSession)module._Session;
        private readonly List<IDisposable> hooks = new();
        private IDisposable gravityLease;
        private bool loaded;

        private PurpleBoosterRuntime(ISourceEngineContext engine)
        {
            Engine = engine;
            Resources = PurpleBoosterResources.For(engine);
        }

        public void Load(SourceEverestModule owner)
        {
            if (loaded) throw new InvalidOperationException("Purple booster hooks are already loaded.");
            var previousModule = module;
            module = owner ?? throw new ArgumentNullException(nameof(owner));
            loaded = true;
            try
            {
                gravityLease = ((ISourceModInteropEnvironment)Engine).ModInterop.RegisterOwned(
                    new SourceModInteropActualTypeBinding(typeof(PurpleBoosterGravityImports), Gravity,
                        exportedMethods: Array.Empty<MethodInfo>()));
                EverestPlayerSourceEvents.OnRegisterStates += RegisterStates;
                hooks.Add(new ILHook(typeof(PlayerContacts).GetMethod(nameof(PlayerContacts.WallJumpCheck)),
                    WallJumpCheck));
                LoadLavenderHooks();
                LoadLegacyConversion();
            }
            catch
            {
                Dispose();
                module = previousModule;
                throw;
            }
        }

        private void RegisterStates(RoomPlayerSession player)
        {
            if (!ReferenceEquals(player.EngineContext, Engine)) return;
            player.Add(new PurpleBoosterPlayerState(this, player));
        }

        public bool IsPlayerInverted() => Gravity.Inverted;

        public List<object> GetBoosters(RoomPlayerSession player) =>
            ((ISourceScene)player.Scene).SourceScene.Entities.GetTrackedEntities(typeof(PurpleBooster));

        private bool Own(PlayerContacts contacts) =>
            contacts.ActualOwner is RoomPlayerSession p && ReferenceEquals(p.EngineContext, Engine);

        private void WallJumpCheck(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After,
                i => i.MatchCallvirt<RoomPlayerSession>("get_" + nameof(RoomPlayerSession.DashAttacking)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, PlayerContacts, bool>>((value, p) =>
                value || (Own(p) && Session.BoosterQoL));
            cursor.GotoNext(MoveType.After, i => i.MatchLdcR4(-1f) && i.Next.MatchCeq());
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, PlayerContacts, float>>((value, p) =>
                Own(p) && Session.BoosterQoL ? p.State.Direction.Y : value);
        }

        public void Dispose()
        {
            if (!loaded) return;
            loaded = false;
            UnloadLegacyConversion();
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            EverestPlayerSourceEvents.OnRegisterStates -= RegisterStates;
            gravityLease?.Dispose();
            gravityLease = null;
        }
    }
}
