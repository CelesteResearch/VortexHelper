using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Simulation.Gameplay.Player.Runtime;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Everest.Runtime.Events.Player;
using Celeste.Mods.VortexHelper.Entities.Boosters;
using Celeste.Scene.Tracking;
using Celeste.Physics.Collision;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Environment.Core;
using Celeste.Simulation.Gameplay.Player.Movement;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.VortexHelper.Runtime.Boosters
{
    public sealed class FloorBoosterHooks : IDisposable
    {
        private static readonly string sourcePlayerRegistryKey =
            typeof(global::Celeste.Simulation.RoomPlayerSession).FullName;

        [ThreadStatic] private static RoomPlayerSession currentPlayer;
        private static readonly Func<PlayerFrameMotion, RoomPlayerWorld> frameWorld = CreateFrameWorldReader();

        private static Func<PlayerFrameMotion, RoomPlayerWorld> CreateFrameWorldReader()
        {
            var method = new DynamicMethod("ReadFloorBoosterFrameWorld", typeof(RoomPlayerWorld),
                new[] { typeof(PlayerFrameMotion) }, typeof(FloorBoosterHooks), true);
            var il = method.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            il.Emit(System.Reflection.Emit.OpCodes.Ldfld,
                typeof(PlayerFrameMotion).GetField("world", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(System.Reflection.Emit.OpCodes.Isinst, typeof(RoomPlayerWorld));
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
            return (Func<PlayerFrameMotion, RoomPlayerWorld>)method.CreateDelegate(
                typeof(Func<PlayerFrameMotion, RoomPlayerWorld>));
        }

        private readonly ISourceEngineContext engine;
        private readonly PurpleBoosterRuntime runtime;
        private readonly List<IDisposable> hooks = new();

        public FloorBoosterHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            runtime = PurpleBoosterRuntime.For(engine);
            try
            {
                EverestPlayerSourceEvents.OnRegisterStates += RegisterPlayerState;
                if (engine.Frame.Scenes.CurrentScene is OriginalLevelSceneRoot level &&
                    ReferenceEquals(level.EngineContext, engine))
                    foreach (RoomPlayerSession player in level.SourceScene.Entities.GetTrackedEntities(
                                 sourcePlayerRegistryKey))
                        RegisterPlayerState(player);
                hooks.Add(new Hook(
                    typeof(NormalSequence).GetMethod("UpdateCore", BindingFlags.Instance | BindingFlags.NonPublic),
                    (Func<Func<NormalSequence, int>, NormalSequence, int>)Update));
                hooks.Add(new Hook(typeof(NormalSequence).GetMethod(nameof(NormalSequence.Begin)),
                    (Action<Action<NormalSequence>, NormalSequence>)Begin));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.RefillDash)),
                    (Func<Func<RoomPlayerSession, bool>, RoomPlayerSession, bool>)Refill));
                hooks.Add(new Hook(typeof(PlayerFrameMotion).GetMethod(nameof(PlayerFrameMotion.RefillDash)),
                    (Func<Func<PlayerFrameMotion, bool>, PlayerFrameMotion, bool>)RefillFrame));
                hooks.Add(new ILHook(typeof(NormalMotion).GetMethod(nameof(NormalMotion.UpdateHorizontal)), Friction));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Own(RoomPlayerSession player) => player != null && ReferenceEquals(player.EngineContext, engine);

        private void RegisterPlayerState(RoomPlayerSession player)
        {
            if (Own(player) && player.Get<FloorBoosterPlayerState>() == null)
                player.Add(new FloorBoosterPlayerState());
        }

        private static FloorBoosterPlayerState State(RoomPlayerSession player) =>
            player.Get<FloorBoosterPlayerState>() ??
            throw new InvalidOperationException(
                "The owned floor booster player state was not registered during construction.");

        private static List<object> Boosters(RoomPlayerSession player) =>
            ((ISourceScene)player.Scene).SourceScene.Entities.GetTrackedEntities(typeof(FloorBooster));

        private static bool Contact(RoomPlayerSession player, FloorBooster booster) =>
            SourceEntityCollision.Check(player, booster) && player.OnGround() &&
            (booster.Ceiling ? player.Top >= booster.Top : player.Bottom <= booster.Bottom);

        private void Begin(Action<NormalSequence> orig, NormalSequence sequence)
        {
            orig(sequence);
            var player = (sequence.World as RoomPlayerWorld)?.SourcePlayer;
            if (!Own(player)) return;
            var state = State(player);
            state.Speed = 0f;
            state.Last = null;
        }

        private int Update(Func<NormalSequence, int> orig, NormalSequence sequence)
        {
            var player = (sequence.World as RoomPlayerWorld)?.SourcePlayer;
            if (!Own(player)) return orig(sequence);
            var state = State(player);
            bool inverted = runtime.IsPlayerInverted();
            var last = state.Last;
            if (last != null && (last.Ceiling != inverted || !SourceEntityCollision.Check(player, last)))
            {
                Vector2 velocity = Vector2.UnitX * state.Speed * (last.Facing == 1 ? last.MoveSpeed : -last.MoveSpeed);
                if (player.OnGround()) player.LiftSpeed += velocity / 1.6f;
                player.Speed += velocity / 1.6f;
                state.Last = null;
            }

            bool touched = false;
            float speed = 0f;
            foreach (FloorBooster booster in Boosters(player))
            {
                if (booster.IceMode || booster.Ceiling != inverted || !Contact(player, booster) ||
                    player.Machine.State == 1) continue;
                if (!touched)
                {
                    speed = ScalarMath.Approach(state.Speed, 1f, 4f * engine.DeltaTime);
                    touched = true;
                }

                player.MoveH(booster.MoveSpeed * booster.Facing * speed * engine.DeltaTime);
                state.Last = booster;
            }

            if (!touched) speed = ScalarMath.Approach(state.Speed, 0f, 4f * engine.DeltaTime);
            state.Speed = speed;
            var previous = currentPlayer;
            currentPlayer = player;
            try
            {
                return orig(sequence);
            }
            finally
            {
                currentPlayer = previous;
            }
        }

        private bool Refill(Func<RoomPlayerSession, bool> orig, RoomPlayerSession player) =>
            BlockRefill(player) ? false : orig(player);

        private bool RefillFrame(Func<PlayerFrameMotion, bool> orig, PlayerFrameMotion frame) =>
            BlockRefill(frameWorld(frame)?.SourcePlayer) ? false : orig(frame);

        private bool BlockRefill(RoomPlayerSession player)
        {
            if (!Own(player) || player.Scene == null || player.Dead ||
                ((OriginalLevelSceneRoot)player.Scene).Transitioning) return false;
            bool inverted = runtime.IsPlayerInverted();
            foreach (FloorBooster booster in Boosters(player))
                if (booster.IceMode && booster.Ceiling == inverted && Contact(player, booster) &&
                    booster.NoRefillsOnIce)
                    return true;
            return false;
        }

        private void Friction(ILContext il)
        {
            var cursor = new ILCursor(il);
            // Preserve the original constant and every other Mod's IL anchors.
            cursor.GotoNext(MoveType.After, i => i.MatchLdcR4(0.65f));
            cursor.GotoNext(MoveType.Before, i => i.MatchStloc(0));
            cursor.EmitDelegate<Func<float, float>>(value => value * GetFriction());
        }

        private float GetFriction()
        {
            var player = currentPlayer;
            if (!Own(player)) return 1f;
            player =
                ((ISourceScene)player.Scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>(
                    sourcePlayerRegistryKey);
            if (player == null) return 1f;
            bool inverted = runtime.IsPlayerInverted();
            foreach (FloorBooster booster in Boosters(player))
                if (booster.IceMode && booster.Ceiling == inverted && Contact(player, booster) &&
                    player.Machine.State != 1)
                    return ((OriginalLevelSceneRoot)player.Scene).CoreMode == RoomCoreMode.Cold ? .4f : .2f;
            return 1f;
        }

        public void Dispose()
        {
            EverestPlayerSourceEvents.OnRegisterStates -= RegisterPlayerState;
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
