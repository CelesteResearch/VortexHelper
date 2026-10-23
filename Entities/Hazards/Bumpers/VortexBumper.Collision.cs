using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Mathematics;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Simulation;

namespace Celeste.Mods.VortexHelper.Entities.Hazards.Bumpers
{
    public partial class VortexBumper
    {
        private void OnPlayer(RoomPlayerSession player)
        {
            var level = (OriginalLevelSceneRoot)Scene;
            if (fireMode)
            {
                if (!((IOriginalGameplayProcess)engine).SaveContext.CurrentSave.Assists.Invincible)
                {
                    Vector2 direction =
                        ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(player.Center - Center);
                    hitDir = -direction;
                    hitWiggler.Start();
                    ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                        "event:/game/09_core/hotpinball_activate", Position);
                    respawnTimer = .6f;
                    player.Die(direction);
                    level.Particles.Emit(((IOriginalGameplayProcess)engine).ParticleTypes.Bumper.P_FireHit, 12,
                        Center + direction * 12f, Vector2.One * 3f, Vector2Geometry.Angle(direction));
                }
            }
            else if (respawnTimer <= 0f)
            {
                ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                    "event:/game/06_reflection/pinballbumper_hit", Position);
                respawnTimer = .6f;
                Vector2 direction = player.ExplodeLaunch(Position, snapUp: false, sidesOnly: false);
                if (twoDashes) player.Data.Dashes = 2;
                if (oneUse) deadly = true;
                sprite.Play("hit", restart: true);
                spriteEvil.Play("hit", restart: true);
                light.Visible = false;
                bloom.Visible = false;
                level.DirectionalShake(direction, .15f);
                level.Displacement.AddBurst(Center, .3f, 8f, 32f, .8f);
                level.Particles.Emit(launch, 12, Center + direction * 12f, Vector2.One * 3f,
                    Vector2Geometry.Angle(direction));
            }
        }
    }
}
