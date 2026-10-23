using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Components;
using Celeste.Graphics.Content;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Core.Reflection.Runtime;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Components.Oscillation;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content.Atlas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Entities.Interaction.Boosters;
using Celeste.Game.Execution.Startup;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Dash;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Celeste.Simulation.Presentation.Effects.Mirrors;
using Celeste.Simulation.Presentation.Particles;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.VortexHelper.Runtime.Boosters;
using Player = Celeste.Simulation.RoomPlayerSession;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using Color = Celeste.Core.Display.PackedColor;

namespace Celeste.Mods.VortexHelper.Entities.Boosters
{
    [CustomEntity("VortexHelper/PurpleBooster")]
    [SceneTracked]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.PurpleBooster")]
    public class PurpleBooster : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly PurpleBoosterRuntime runtime;
        private readonly PurpleBoosterResources resources;

        private void Play(string path, Vector2 position) =>
            ((IOriginalGameplayProcess)engine).AudioSession.Play(path, position,
                ((IOriginalGameplayProcess)engine).SaveContext);

        private readonly Sprite sprite;
        private readonly WigglerComponent wiggler;
        private SceneEntityState outline;

        private readonly AtlasEntry linkSegCenter, linkSegCenterOutline, linkSeg, linkSegOutline;

        private readonly SimulationCoroutine dashRoutine;
        private readonly DashListenerState dashListener;

        private float respawnTimer;
        private float cannotUseTimer;
        public bool BoostingPlayer { get; set; }
        public bool StartedBoosting;

        private bool linkVisible = false;
        private float actualLinkPercent = 1.0f;
        private float linkPercent = 1.0f;

        private ParticleType P_Burst => resources.Burst;
        private ParticleType P_Appear => resources.Appear;
        private ParticleType P_BurstExplode => resources.BurstExplode;

        private readonly SoundSourceComponentState loopingSfx;
        public readonly bool QoL;
        public readonly bool LegacyGravityHelper;

        public PurpleBooster(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine, data.Position + offset, data.Attr("spriteOverride", null))
        {
            QoL = data.Bool("QoL");

            // Unlike most legacy settings, this defaults to false so that old maps used the "fixed" functionality.
            // If an older map specifically wants the jank interaction, they will need to replace the entities and rerelease.
            LegacyGravityHelper = data.Bool("legacyGravityHelper", false);
        }

        public PurpleBooster(ISourceEngineContext engine, Vector2 position, string spriteOverride)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            this.engine = engine;
            this.runtime = PurpleBoosterRuntime.For(engine);
            this.resources = PurpleBoosterResources.For(engine);
            this.Depth = -8500;
            this.Collider =
                new CircleColliderGeometry(((ISourceVectorEnvironment)engine).VectorArithmetic, 10f, 0f, 2f);
            if (!string.IsNullOrWhiteSpace(spriteOverride))
            {
                spriteOverride = spriteOverride.Trim().TrimEnd('/') + "/";
                this.sprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                    spriteOverride);
                sprite.CenterOrigin();
                sprite.Add("appear", "appear", 0.04f);
                sprite.AddLoop("loop", "loop", 0.1f);
                sprite.AddLoop("inside", "inside", 0.1f);
                sprite.AddLoop("spin", "spin", 0.1f);
                sprite.Add("appear", "appear", 0.04f);
                this.linkSegCenter =
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[spriteOverride + "link03"];
                this.linkSegCenterOutline =
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[spriteOverride + "link02"];
                this.linkSeg =
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[spriteOverride + "link01"];
                this.linkSegOutline =
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[spriteOverride + "link00"];
            }
            else
            {
                this.sprite = resources.PurpleSprites.Create("purpleBooster");
                this.linkSegCenter =
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[
                        "objects/VortexHelper/slingBooster/link03"];
                this.linkSegCenterOutline =
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[
                        "objects/VortexHelper/slingBooster/link02"];
                this.linkSeg =
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[
                        "objects/VortexHelper/slingBooster/link01"];
                this.linkSegOutline =
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[
                        "objects/VortexHelper/slingBooster/link00"];
            }

            OwnedComponents.Add(this.sprite);

            OwnedComponents.Add(new PlayerColliderComponentState(OnPlayer));
            OwnedComponents.Add(new VertexLightState(engine, Color.White, 1f, 16, 32));
            OwnedComponents.Add(new BloomPointState(0.1f, 16f));
            OwnedComponents.Add(this.wiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(0.5f, 4f,
                delegate(float f) { this.sprite.Scale = Vector2.One * (1f + f * 0.25f); }));

            OwnedComponents.Add(this.dashRoutine = new SimulationCoroutine(engine, removeOnComplete: false));
            OwnedComponents.Add(this.dashListener = new DashListenerState());

            OwnedComponents.Add(new MirrorReflectionState());
            OwnedComponents.Add(this.loopingSfx = ((ISourceSoundSourceEnvironment)engine).SoundSources.Create());

            this.dashListener.OnDash = OnPlayerDashed;
        }

        public override void Added(object scene)
        {
            base.Added(scene);

            var image = new SpriteImage(
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas["objects/booster/outline"]);
            image.CenterOrigin();
            image.Color = Color.White * 0.75f;

            this.outline =
                new SceneEntityState(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                    this.Position)
                {
                    Depth = 8999,
                    Visible = false
                };
            this.outline.Y += 2f;
            this.outline.OwnedComponents.Add(image);
            this.outline.OwnedComponents.Add(new MirrorReflectionState());
            ((ISourceScene)scene).SourceScene.QueueSourceEntity(this.outline);
        }

        private void AppearParticles()
        {
            ParticleSystemPresentation particlesBG = ((OriginalLevelSceneRoot)Scene).ParticlesBG;
            for (int i = 0; i < 360; i += 30)
                particlesBG.Emit(P_Appear, 1, this.Center, Vector2.One * 2f, i * ((float)Math.PI / 180f));
        }

        private void OnPlayer(Player player)
        {
            if (this.respawnTimer <= 0f && this.cannotUseTimer <= 0f && !this.BoostingPlayer)
            {
                this.linkPercent = this.actualLinkPercent = 1.0f;
                this.linkVisible = false;

                this.cannotUseTimer = 0.45f;

                Boost(player, this);

                Play("event:/game/04_cliffside/greenbooster_enter", this.Position);
                this.wiggler.Start();
                this.sprite.Play("inside", false, false);
            }
        }

        public static void Boost(Player player, PurpleBooster booster)
        {
            var state = player.Get<PurpleBoosterPlayerState>();
            player.Machine.State = state.BoostState;
            player.Speed = Vector2.Zero;
            player.BoostTarget = booster.Center;
            state.Runtime.Session.BoosterQoL = booster.QoL;
            state.Runtime.Session.BoosterLegacyGravityHelper = booster.LegacyGravityHelper;
            booster.StartedBoosting = true;
        }

        public void PlayerBoosted(Player player, Vector2 direction)
        {
            this.StartedBoosting = false;
            this.BoostingPlayer = false;
            this.linkVisible = true;

            Play("event:/game/04_cliffside/greenbooster_dash", this.Position);

            this.loopingSfx.Play("event:/game/05_mirror_temple/redbooster_move");
            this.loopingSfx.DisposeOnTransition = false;

            this.BoostingPlayer = true;
            this.Tags = ((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value |
                        ((IOriginalGameTagsEnvironment)engine).Tags.TransitionUpdate.Value;

            this.sprite.Play("spin", false, false);
            this.wiggler.Start();
            this.dashRoutine.Replace(BoostRoutine(player, direction));
        }

        private IEnumerator BoostRoutine(Player player, Vector2 dir)
        {
            var level = (OriginalLevelSceneRoot)Scene;

            int dashState = player.Get<PurpleBoosterPlayerState>().DashState;
            while (player.Machine.State == dashState && this.BoostingPlayer)
            {
                if (player.Dead)
                    PlayerDied();
                else
                {
                    this.sprite.RenderPosition = player.Center;
                    this.loopingSfx.Position = this.sprite.Position;

                    if (((ISourceScene)this.Scene).SourceScene.Clock.OnInterval(0.02f, engine))
                        level.ParticlesBG.Emit(P_Burst, 2, player.Center - dir * 3f + new Vector2(0f, -2f),
                            new Vector2(3f, 3f));

                    yield return null;
                }
            }

            PlayerReleased();

            if (player.Machine.State == 4)
            {
                this.sprite.Visible = false;
            }

            this.linkVisible = player.Machine.State is 2 or 0;
            this.linkPercent = this.linkVisible ? 0.0f : 1.0f;

            if (!this.linkVisible && !player.Dead)
                LaunchPlayerParticles(player, -dir, P_BurstExplode);

            while (((OriginalLevelSceneRoot)Scene).Transitioning)
                yield return null;

            this.Tags = 0;
            yield break;
        }

        private void OnPlayerDashed(Vector2 direction)
        {
            if (this.BoostingPlayer)
                this.BoostingPlayer = false;
        }

        internal void PlayerReleased()
        {
            Play("event:/game/05_mirror_temple/redbooster_end", this.sprite.RenderPosition);
            this.sprite.Play("pop");
            this.cannotUseTimer = 0f;
            this.respawnTimer = 1f;
            this.BoostingPlayer = false;
            this.outline.Visible = true;
            this.loopingSfx.Stop();
        }

        private void PlayerDied()
        {
            if (!this.BoostingPlayer)
                return;

            PlayerReleased();
            this.dashRoutine.Active = false;
            this.Tags = 0;
        }

        private void Respawn()
        {
            Play("event:/game/04_cliffside/greenbooster_reappear", this.Position);

            this.sprite.Position = Vector2.Zero;
            this.sprite.Play("appear", restart: true);
            this.sprite.Visible = true;

            this.outline.Visible = false;
            AppearParticles();
        }

        public override void Update()
        {
            base.Update();

            var inverted = !this.LegacyGravityHelper && runtime.IsPlayerInverted();

            this.actualLinkPercent =
                ScalarMath.Approach(this.actualLinkPercent, this.linkPercent, 5f * engine.DeltaTime);

            if (BoosterBubbleMotion.AdvanceCooldowns(engine, ref this.cannotUseTimer, ref this.respawnTimer)) Respawn();

            if (!this.dashRoutine.Active && this.respawnTimer <= 0f)
            {
                Vector2 target = Vector2.Zero;
                Player entity =
                    ((ISourceScene)this.Scene).SourceScene.Entities.GetTrackedEntity<Player>(typeof(Player).FullName);
                if (entity is not null && SourceEntityCollision.Check(this, entity))
                {
                    var playerOffset = new Vector2(OriginalBoosterState.playerOffset.X,
                        inverted ? -OriginalBoosterState.playerOffset.Y : OriginalBoosterState.playerOffset.Y);
                    target = entity.Center + playerOffset - this.Position;
                }

                BoosterBubbleMotion.ApproachSprite(engine, this.sprite, target);
            }

            if (this.sprite.CurrentAnimationID == "inside" && !this.BoostingPlayer &&
                !CollideCheck(typeof(Player).FullName))
                this.sprite.Play("loop");
        }

        public static void LaunchPlayerParticles(Player player, Vector2 dir, ParticleType p)
        {
            var level = (OriginalLevelSceneRoot)player.Scene;
            float angle = Vector2Geometry.Angle(dir) - 0.5f;
            for (int i = 0; i < 20; i++)
                level.ParticlesBG.Emit(p, 1, player.Center, new Vector2(3f, 3f),
                    angle + SourceRandomMath.NextFloat(((ISourceRandomEnvironment)player.EngineContext).Random));
        }

        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = BoosterBubbleMotion.BeginRender(this.sprite, context);
            if (this.linkVisible) RenderPurpleBoosterLink(context, 12, 0.35f);
            base.Render(context);
            BoosterBubbleMotion.EndRender(this.sprite, position);
        }

        private void RenderPurpleBoosterLink(ISourceRenderContext context, int spriteCount, float minScale)
        {
            float increment = 1f / spriteCount;
            float centerSegmentScale = 0.7f + 0.3f * this.actualLinkPercent;

            SourceAtlasDrawing.DrawOutline(context, this, this.linkSegCenterOutline, this.Center,
                this.linkSegCenterOutline.Center, Color.Black, centerSegmentScale);
            for (float t = increment; t <= this.actualLinkPercent; t += increment) // Black Outline
            {
                var vec = Vector2.Lerp(this.Center, this.sprite.RenderPosition, t * this.actualLinkPercent);
                SourceAtlasDrawing.DrawOutline(context, this, this.linkSegOutline, vec, this.linkSegOutline.Center,
                    Color.Black, 1.01f - t * minScale);
            }

            SourceAtlasDrawing.DrawCentered(context, this, this.linkSegCenterOutline, this.Center, Color.White,
                centerSegmentScale);
            for (float t = increment; t <= this.actualLinkPercent; t += increment) // Pink Outline
            {
                var vec = Vector2.Lerp(this.Center, this.sprite.RenderPosition, t * this.actualLinkPercent);
                SourceAtlasDrawing.DrawCentered(context, this, this.linkSegOutline, vec, Color.White,
                    1.01f - t * minScale);
            }

            SourceAtlasDrawing.DrawCentered(context, this, this.linkSegCenter, this.Center, Color.White,
                centerSegmentScale); // Sprites
            for (float t = increment; t <= this.actualLinkPercent; t += increment)
            {
                var vec = Vector2.Lerp(this.Center, this.sprite.RenderPosition, t * this.actualLinkPercent);
                SourceAtlasDrawing.DrawCentered(context, this, this.linkSeg, vec, Color.White, 1f - t * minScale);
            }
        }
    }
}
