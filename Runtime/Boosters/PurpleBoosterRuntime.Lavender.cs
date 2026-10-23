using System;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Everest.Runtime.Levels.Update;
using Celeste.Everest.Runtime.Levels.Loading.Entities;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Entities.Interaction.Boosters;
using Celeste.Game.Execution.Processes;
using Celeste.Mods.VortexHelper.Entities.Boosters;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.VortexHelper.Runtime.Boosters
{
    public sealed partial class PurpleBoosterRuntime
    {
        private void LoadLavenderHooks()
        {
            hooks.Add(new Hook(typeof(OriginalBoosterState).GetMethod(nameof(OriginalBoosterState.PlayerReleased)),
                (Action<Action<OriginalBoosterState>, OriginalBoosterState>)PlayerReleased));
            hooks.Add(new Hook(
                typeof(OriginalBoosterState).GetMethod("AppearParticles",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                (Action<Action<OriginalBoosterState>, OriginalBoosterState>)AppearParticles));
        }

        private bool Own(OriginalBoosterState booster) => booster.Scene is ISourceEngineContextScene scene &&
                                                          ReferenceEquals(scene.EngineContext, Engine);

        private void PlayerReleased(Action<OriginalBoosterState> original, OriginalBoosterState booster)
        {
            original(booster);
            if (!Own(booster)) return;
            var player = (Engine.Frame.Scenes.CurrentScene as ISourceScene)?.SourceScene.Entities
                .GetTrackedEntity<RoomPlayerSession>();
            if (player?.LastBooster is not LavenderBooster lavender) return;
            ((IOriginalGameplayProcess)Engine).AudioSession.Play("event:/game/05_mirror_temple/redbooster_end",
                player.Center, ((IOriginalGameplayProcess)Engine).SaveContext);
            PurpleBooster.LaunchPlayerParticles(player, player.Data.Direction, Resources.LavenderBurstExplode);
            Session.BoosterQoL = lavender.QoL;
            Session.BoosterLegacyGravityHelper = lavender.LegacyGravityHelper;
            player.Get<PurpleBoosterPlayerState>().ExplodeLaunch(booster.Center - player.Data.Direction, null, -1f);
        }

        private void AppearParticles(Action<OriginalBoosterState> original, OriginalBoosterState booster)
        {
            if (!Own(booster) || booster is not LavenderBooster)
            {
                original(booster);
                return;
            }

            var particles = ((OriginalLevelSceneRoot)booster.Scene).ParticlesBG;
            for (int i = 0; i < 360; i += 30)
                particles.Emit(Resources.Appear, 1, booster.Center, Vector2.One * 2f, i * ((float)Math.PI / 180f));
        }

        private void LoadLegacyConversion() =>
            ((IEverestLevelEntityLoadEnvironment)Engine).LevelUpdateEvents.OnLoadEntity += ConvertLavender;

        private void UnloadLegacyConversion() =>
            ((IEverestLevelEntityLoadEnvironment)Engine).LevelUpdateEvents.OnLoadEntity -= ConvertLavender;

        private bool ConvertLavender(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
        {
            if (data.Name != "VortexHelper/PurpleBooster" || !data.Bool("lavender")) return false;
            data.Name = "VortexHelper/LavenderBooster";
            return ((IEverestLevelCustomEntityLoaderEnvironment)Engine).CustomEntities.LoadCustomEntity(data, level);
        }
    }
}
