using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Scene.Tracking;

namespace Celeste.Mods.VortexHelper.Entities.Hazards.Bumpers
{
    public partial class VortexBumper
    {
        private void UpdatePosition() =>
            Position = anchor + new Vector2(sine.Value * 3f, sine.ValueOverTwo * 2f) * (wobble ? 1f : 0f);

        public override void Update()
        {
            base.Update();
            if (respawnTimer > 0f)
            {
                respawnTimer -= engine.DeltaTime;
                if (respawnTimer <= 0f)
                {
                    light.Visible = true;
                    bloom.Visible = true;
                    sprite.Play("on");
                    spriteEvil.Play("on");
                    if (deadly)
                    {
                        fireMode = true;
                        spriteEvil.Visible = fireMode;
                        sprite.Visible = !fireMode;
                    }

                    ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                        "event:/game/06_reflection/pinballbumper_reset", Position);
                }
            }
            else if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(.05f, 0f, engine))
            {
                float angle = SourceRandomMath.NextAngle(((ISourceRandomEnvironment)engine).Random);
                float direction = fireMode ? -(float)Math.PI / 2f : angle;
                float length = fireMode ? 12 : 8;
                var type = fireMode ? ((IOriginalGameplayProcess)engine).ParticleTypes.Bumper.P_FireAmbience : ambience;
                ((OriginalLevelSceneRoot)Scene).Particles.Emit(type, 1,
                    Center + Vector2Geometry.AngleToVector(angle, length), Vector2.One * 2f, direction);
            }

            UpdatePosition();
        }
    }
}
