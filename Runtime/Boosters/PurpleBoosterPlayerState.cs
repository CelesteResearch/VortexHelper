using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Dash;
using Celeste.Simulation.Gameplay.Player.Scene;
using Celeste.Simulation.Gameplay.Player.Scene.Effects;
using Celeste.Mods.VortexHelper.Entities.Boosters;

namespace Celeste.Mods.VortexHelper.Runtime.Boosters
{
    public sealed class PurpleBoosterPlayerState : SourceComponentState
    {
        private readonly RoomPlayerSession player;
        private readonly ISourceEngineContext engine;
        public PurpleBoosterRuntime Runtime { get; }
        public int BoostState { get; }
        public int DashState { get; }
        private Vector2 EarlyDashSpeed;

        public PurpleBoosterPlayerState(PurpleBoosterRuntime runtime, RoomPlayerSession player) : base(false, false)
        {
            this.player = player;
            Runtime = runtime;
            engine = runtime.Engine;
            var machine = (ISourceNamedStateMachine)player.Machine;
            BoostState = machine.AddState("VortexHelper/PurpleBoost", PurpleBoostUpdate, PurpleBoostCoroutine,
                PurpleBoostBegin, PurpleBoostEnd);
            DashState = machine.AddState("VortexHelper/PurpleDash", PurpleDashingUpdate, PurpleDashingCoroutine,
                PurpleDashingBegin, PurpleDashingEnd);
        }

        private static Vector2 Sign(Vector2 value) => new(Math.Sign(value.X), Math.Sign(value.Y));
        private static float QuadIn(float value) => value * value;
        private static float QuadOut(float value) => 1 - (1 - value) * (1 - value);

        private void PurpleBoostBegin()
        {
            player.CurrentBooster = null;

            // Fixes hair sticking out of the bubble sprite when entering it ducking.
            // If for whatever reason this breaks an older map, this will be removed.
            player.Data.Motion.Ducking = false;

            var level = (OriginalLevelSceneRoot)player.Scene;

            bool? flag2 = (level as ISourcePlayerBoostMetadataScene)?.BoostTheoInBubble;
            player.RefillDash();
            player.RefillStamina();

            if (flag2.GetValueOrDefault())
                return;

            player.Drop();
        }

        private int PurpleBoostUpdate()
        {
            Vector2 boostTarget = player.BoostTarget;
            Vector2 value = player.Input.Aim.Value * 3f;
            Vector2 vector = PlayerBoostMotion.ApproachPosition(engine, player.Body.ExactPosition, boostTarget,
                player.Collider.Center, value);

            var legacyGravityHelper = Runtime.Session.BoosterLegacyGravityHelper;
            if (!legacyGravityHelper) Runtime.Gravity.BeginOverride?.Invoke();
            player.MoveToX(vector.X, null);
            player.MoveToY(vector.Y, null);
            if (!legacyGravityHelper) Runtime.Gravity.EndOverride?.Invoke();

            if (Vector2.DistanceSquared(player.Center, boostTarget) >= 275f)
            {
                foreach (PurpleBooster b in Runtime.GetBoosters(player))
                    if (b.StartedBoosting)
                        b.PlayerReleased();
                return 0;
            }

            // now supports demobutton presses
            if (player.Input.DashPressed || player.Input.CrouchDashPressed)
            {
                // we don't need to do this, we're not actually dashing here- but fastbubbling.
                //demoDashed = player.Input.CrouchDashPressed;
                player.Input.Dash.ConsumePress();
                player.Input.CrouchDash.ConsumeBuffer();
                return DashState;
            }

            return BoostState;
        }

        private void PurpleBoostEnd()
        {
            var legacyGravityHelper = Runtime.Session.BoosterLegacyGravityHelper;
            if (!legacyGravityHelper) Runtime.Gravity.BeginOverride?.Invoke();
            Vector2 vector = PlayerBoostMotion.ReleasePosition(player.BoostTarget, player.Collider.Center);
            player.MoveToX(vector.X, null);
            player.MoveToY(vector.Y, null);
            if (!legacyGravityHelper) Runtime.Gravity.EndOverride?.Invoke();
        }

        private IEnumerator PurpleBoostCoroutine()
        {
            yield return 0.3f;
            player.Machine.State = DashState;
        }

        // Arc Motion 
        private void PurpleDashingBegin()
        {
            player.Freeze(0.05f); // this freeze makes fastbubbling much more lenient
            player.Data.Direction = player.Input.GetAimVector(player.Facing);
            if (!Runtime.Session.BoosterLegacyGravityHelper)
                player.Data.Direction = Runtime.Gravity.InvertIfRequired(player.Data.Direction);
            EarlyDashSpeed = Vector2.Zero;

            foreach (PurpleBooster b in Runtime.GetBoosters(player))
            {
                if (b.StartedBoosting)
                {
                    b.PlayerBoosted(player, player.Data.Direction);
                    return;
                }

                if (b.BoostingPlayer)
                    return;
            }
        }

        private int PurpleDashingUpdate()
        {
            bool QoL = Runtime.Session.BoosterQoL;
            if (player.Input.DashPressed || player.Input.CrouchDashPressed)
            {
                if (QoL) player.Speed += EarlyDashSpeed;
                else player.LiftSpeed += EarlyDashSpeed;
                return player.StartDash();
            }

            if (QoL && Math.Abs(player.Data.Direction.X) <= 0.02 &&
                player.Input.Jump.Pressed && player.World.CanUnDuck &&
                (player.Data.Direction.Y < 0 ? EarlyDashSpeed.Y == 0 : EarlyDashSpeed.Y < 0))
            {
                if (player.World.WallJumpCheck(1))
                {
                    player.SuperWallJump(-1);
                    return 0;
                }
                else if (player.World.WallJumpCheck(-1))
                {
                    player.SuperWallJump(1);
                    return 0;
                }
            }

            return DashState;
        }

        private IEnumerator PurpleDashingCoroutine()
        {
            float t = 0f;
            Vector2 origin = player.BoostTarget;
            if (Runtime.Session.BoosterQoL)
            {
                yield return null;
                player.Data.Direction = player.Data.LastAim;
                if (!Runtime.Session.BoosterLegacyGravityHelper)
                    player.Data.Direction = Runtime.Gravity.InvertIfRequired(player.Data.Direction);
            }

            Vector2 earlyExitBoost = Vector2.Zero;
            while (t < 1f)
            {
                t = ScalarMath.Approach(t, 1.0f, engine.DeltaTime * 1.5f);
                var inverted = !Runtime.Session.BoosterLegacyGravityHelper && Runtime.Gravity.Inverted;
                float offset = inverted ? -6f : 6f;
                Vector2 vec = origin + Vector2.UnitY * offset +
                              player.Data.Direction * 60f * (float)Math.Sin(t * Math.PI);

                if (Runtime.Session.BoosterQoL)
                {
                    if (t == 1f)
                    {
                        // frame 0: mimics speed at launch exit exactly, player.Input.MoveX.Value == -Math.Sign(player.Data.Direction) ? 300 : 250
                        earlyExitBoost = 250f * -player.Data.Direction;
                        Vector2 aim = player.Input.GetAimVector(player.Facing);
                        if (!Runtime.Session.BoosterLegacyGravityHelper)
                            aim = Runtime.Gravity.InvertIfRequired(aim);
                        aim = Sign(Vector2Geometry.EightWayNormal(aim));
                        if (aim.X == Math.Sign(earlyExitBoost.X)) earlyExitBoost.X *= 1.2f;
                        if (aim.Y == Math.Sign(earlyExitBoost.Y)) earlyExitBoost.Y *= 1.2f;
                    }
                    else if (t > 0.93f)
                    {
                        // frame -2 : 200 speed
                        // frame -1 : 205 speed
                        earlyExitBoost = (float)Math.Round(210f * t) * -player.Data.Direction;
                    }
                }
                else if (t > 0.6f)
                {
                    earlyExitBoost = (t - .5f) * 200f * -player.Data.Direction;
                }

                EarlyDashSpeed = earlyExitBoost;

                if (player.Body.CollideSolidAt(vec))
                {
                    player.Machine.State = 0;
                    yield break;
                }

                var legacyGravityHelper = Runtime.Session.BoosterLegacyGravityHelper;
                if (!legacyGravityHelper) Runtime.Gravity.BeginOverride?.Invoke();
                player.MoveToX(vec.X);
                player.MoveToY(vec.Y);
                if (!legacyGravityHelper) Runtime.Gravity.EndOverride?.Invoke();
                yield return null;
            }

            player.LiftSpeed += 120f * -player.Data.Direction;
            ExplodeLaunch(player.Center - player.Data.Direction, origin);
        }

        private void PurpleDashingEnd()
        {
            Runtime.Session.BoosterQoL = false;
            Runtime.Session.BoosterLegacyGravityHelper = false;
        }

        public void ExplodeLaunch(Vector2 from, Vector2? origin, float factor = 1f)
        {
            bool QoL = Runtime.Session.BoosterQoL;
            bool legacyGravityHelper = Runtime.Session.BoosterLegacyGravityHelper;
            ((ISourceRumbleEnvironment)engine).RumbleSession.Rumble(2, 1);
            player.Freeze(QoL ? 0.05f : 0.1f);
            player.Special.LaunchApproachX = null;
            var level = (OriginalLevelSceneRoot)player.Scene;

            if (origin is not null)
                level.Displacement.AddBurst((Vector2)origin, 0.25f, 8f, 64f, 0.5f, QuadIn, QuadOut);

            level.Shake(0.15f);

            Vector2 vector =
                ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(player.Center - from, -Vector2.UnitY);
            if (!legacyGravityHelper) vector = Runtime.Gravity.InvertIfRequired(vector);

            if (Math.Abs(vector.X) < 1f && Math.Abs(vector.Y) < 1f)
                vector *= 1.1f;

            player.Speed = 250f * -vector;

            Vector2 aim = Sign(Vector2Geometry.EightWayNormal(player.Input.GetAimVector(player.Facing)));
            if (aim.X == Math.Sign(player.Speed.X)) player.Data.Motion.Normal.Speed.X *= 1.2f;
            if (aim.Y == Math.Sign(player.Speed.Y)) player.Data.Motion.Normal.Speed.Y *= 1.2f;

            ((ISourcePlayerDashEffectsScene)player.Scene).CreatePlayerDashSlash(player.Center,
                Vector2Geometry.Angle(player.Speed));
            if (!player.World.NoRefills)
                player.RefillDash();
            player.RefillStamina();
            player.Data.CooldownTimer = Math.Min(player.Data.CooldownTimer, 0.06f);
            player.Machine.State = 7;
            player.Speed *= factor;
        }
    }
}
