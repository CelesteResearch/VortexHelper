using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Components.Animation;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Game.Components.Switches;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Mechanisms.SwitchGates;
using Celeste.Game.Gameplay.Mechanisms.SwitchGates.Effects;
using Celeste.Game.Gameplay.Mechanisms.SwitchGates.Presentation;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Actors.Debris;
using Celeste.Simulation.Presentation.Lighting;

namespace Celeste.Mods.VortexHelper.Entities.Gates
{
    public partial class VortexSwitchGate
    {
        private IEnumerator ShatterSequence()
        {
            OriginalLevelSceneRoot level = Level;
            while (!OriginalSwitchComponent.Check(Scene)) yield return null;
            if (persistent) OriginalSwitchComponent.SetLevelFlag(level);
            openSfx.Play(global::Celeste.Game.Audio.OriginalSfxProcess.game_gen_fallblock_shake);
            yield return 0.1f;
            StartShaking(0.5f);
            while (icon.Rate < 1f)
            {
                icon.Color =
                    SourceColorInterpolation.Lerp(inactiveColor, finishColor, icon.Rate, process.ScalarArithmetic);
                icon.Rate += process.DeltaTime * 2f;
                yield return null;
            }

            yield return 0.1f;
            SwitchGateParticleEffects.EmitIconFire(this, iconOffset, process);
            openSfx.Stop();
            process.AudioSession.Play(global::Celeste.Game.Audio.OriginalSfxProcess.game_gen_wallbreak_stone, Center,
                process.SaveContext);
            process.AudioSession.Play(global::Celeste.Game.Audio.OriginalSfxProcess.game_gen_touchswitch_gate_finish,
                Center, process.SaveContext);
            level.Shake();
            for (int x = 0; x < Width / 8f; x++)
            for (int y = 0; y < Height / 8f; y++)
            {
                DebrisState debris = new DebrisState(process, SourceWorld.TargetMotionPolicy)
                    .Init(Position + new Vector2(4 + x * 8, 4 + y * 8), '1').BlastFrom(Center);
                debris.SetTexture(process.GameGraphics.GameAtlas[debrisPath]);
                ((ISourceScene)Scene).SourceScene.QueueSourceEntity(debris);
            }

            DestroyStaticMovers();
            RemoveSelf();
        }

        private IEnumerator CrushSequence(Vector2 node)
        {
            OriginalLevelSceneRoot level = Level;
            Vector2 start = Position;
            while (!OriginalSwitchComponent.Check(Scene)) yield return null;
            if (persistent) OriginalSwitchComponent.SetLevelFlag(level);
            yield return 0.1f;
            StartShaking(0.5f);
            openSfx.Play(global::Celeste.Game.Audio.OriginalSfxProcess.game_gen_touchswitch_gate_open);
            while (icon.Rate < 1f)
            {
                icon.Color =
                    SourceColorInterpolation.Lerp(inactiveColor, activeColor, icon.Rate, process.ScalarArithmetic);
                icon.Rate += process.DeltaTime * 2f;
                yield return null;
            }

            yield return 0.1f;
            int particleAt = 0;
            var tween = SourceTweenState.Create(process.TweenPool, SourceTweenState.TweenMode.Oneshot,
                process.EasingArithmetic.CubeIn, crushSpeed, start: true);
            tween.OnUpdate = t =>
            {
                MoveTo(Vector2Math.Lerp(start, node, t.Eased, process.ScalarArithmetic));
                if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.1f, process))
                {
                    particleAt++;
                    particleAt %= 2;
                    for (int x = 0; x < Width / 8f; x++)
                    for (int y = 0; y < Height / 8f; y++)
                        if ((x + y) % 2 == particleAt)
                            level.ParticlesBG.Emit(process.ParticleTypes.SwitchGate.P_Behind,
                                Position + new Vector2(x * 8, y * 8) +
                                SourceRandomMath.Range(process.Random, Vector2.One * 2f, Vector2.One * 6f));
                }
            };
            Add(tween);
            yield return crushSpeed;
            bool collidable = Collidable;
            Collidable = false;
            SwitchGateParticleEffects.EmitEdgeDust(this, start, node, process);
            Collidable = collidable;
            process.AudioSession.Play(global::Celeste.Game.Audio.OriginalSfxProcess.game_gen_touchswitch_gate_finish,
                Position, process.SaveContext);
            openSfx.Stop();
            StartShaking(0.2f);
            level.Shake();
            while (icon.Rate > 0f)
            {
                icon.Color = SourceColorInterpolation.Lerp(activeColor, finishColor, 1f - icon.Rate,
                    process.ScalarArithmetic);
                icon.Rate -= process.DeltaTime * 4f;
                yield return null;
            }

            icon.Rate = 0f;
            icon.SetAnimationFrame(0);
            wiggler.Start();
            collidable = Collidable;
            Collidable = false;
            if (Celeste.Scene.Entities.SourceSceneCollision.FirstPoint<Celeste.Physics.Collision.ISourceEntityCollider>(
                    ((ISourceScene)Scene).SourceScene.Entities, "Celeste.Solid", Center) == null)
                SwitchGateParticleEffects.EmitIconFire(this, iconOffset, process);
            Collidable = collidable;
        }
    }
}
