using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Rendering;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Graphics.Animation;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Environment.Core;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Mods.VortexHelper.Entities.Blocks;
using Celeste.Mods.VortexHelper.Runtime.Boosters;

namespace Celeste.Mods.VortexHelper.Entities.Boosters
{
    [CustomEntity("VortexHelper/FloorBooster")]
    [SceneTracked(false)]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.FloorBooster")]
    public class FloorBooster : SceneEntityState
    {
        private static readonly string sourcePlayerRegistryKey =
            typeof(global::Celeste.Simulation.RoomPlayerSession).FullName;

        private readonly ISourceEngineContext engine;
        private readonly PurpleBoosterRuntime runtime;

        public static FloorBooster Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            EntityData data) => new(level.EngineContext, data, offset);

        private enum DisableMode
        {
            Disappear,
            ColorFade
        }

        public int Facing;
        private Vector2 imageOffset;
        private readonly SoundSourceComponentState idleSfx, activateSfx;

        private bool isPlaying = false;

        private readonly List<Sprite> tiles;

        private readonly bool notCoreMode;

        public bool IceMode;
        public bool NoRefillsOnIce;
        public bool Ceiling;

        private DisableMode disableMode;

        public PackedColor EnabledColor = PackedColor.White;
        public PackedColor DisabledColor = PackedColor.Lerp(PackedColor.White, PackedColor.Black, 0.5f);

        public int MoveSpeed;

        public FloorBooster(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine, data.Position + offset, data.Width, data.Bool("left"), data.Int("speed"),
                data.Bool("iceMode"), data.Bool("noRefillOnIce"), data.Bool("notAttached"), data.Bool("ceiling"))
        {
        }

        public FloorBooster(ISourceEngineContext engine, Vector2 position, int width, bool left, int speed,
            bool iceMode, bool noRefillOnIce, bool notAttached)
            : this(engine, position, width, left, speed, iceMode, noRefillOnIce, notAttached, false)
        {
        }

        public FloorBooster(ISourceEngineContext engine, Vector2 position, int width, bool left, int speed,
            bool iceMode, bool noRefillOnIce, bool notAttached, bool ceiling)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            this.engine = engine;
            runtime = PurpleBoosterRuntime.For(engine);
            this.Tags = ((IOriginalGameTagsEnvironment)engine).Tags.TransitionUpdate.Value;
            this.Depth = 1999;

            this.NoRefillsOnIce = noRefillOnIce;
            this.notCoreMode = iceMode;
            this.IceMode = iceMode;
            this.MoveSpeed = Math.Max(0, speed);
            this.Facing = left ? -1 : 1;
            this.Ceiling = ceiling;

            this.Collider = new HitboxColliderGeometry(width, 3, 0, ceiling ? 0 : 5);
            if (!this.notCoreMode)
                Add(new CoreModeListenerState(OnChangeMode));

            Add(this.idleSfx = ((ISourceSoundSourceEnvironment)engine).SoundSources.Create());
            Add(this.activateSfx = ((ISourceSoundSourceEnvironment)engine).SoundSources.Create());

            if (!notAttached)
            {
                Add(new SourceStaticMoverState
                {
                    OnShake = OnShake,
                    SolidChecker = IsRiding,
                    JumpThruChecker = IsRiding,
                    OnEnable = OnEnable,
                    OnDisable = OnDisable
                });
            }

            this.tiles = BuildSprite(left, ceiling);
        }

        public void SetColor(PackedColor color)
        {
            foreach (SourceComponentState component in this.OwnedComponents)
            {
                if (component is SpriteImage image)
                {
                    image.Color = color;
                }
            }
        }

        private void OnEnable()
        {
            if (!this.IceMode)
                this.idleSfx.Play("event:/env/local/09_core/conveyor_idle");

            this.Active = this.Collidable = this.Visible = true;
            SetColor(this.EnabledColor);
        }

        private void OnDisable()
        {
            this.idleSfx.Stop();
            PlayActivateSfx(true);

            this.Active = this.Collidable = false;
            if (this.disableMode == DisableMode.ColorFade)
                SetColor(this.DisabledColor);
            else
                this.Visible = false;
        }

        private List<Sprite> BuildSprite(bool left, bool ceiling)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < this.Width; i += 8)
            {
                // Sprite Selection
                string id = (i == 0)
                    ? left
                        ? "Right"
                        : "Left"
                    : (i + 16) > this.Width
                        ? left
                            ? "Left"
                            : "Right"
                        : "Mid";

                Sprite sprite = FloorBoosterResources.For(engine).Sprites.Create("FloorBooster" + id);
                if (!left)
                    sprite.FlipX = true;
                if (ceiling)
                    sprite.FlipY = true;

                sprite.Position = new Vector2(i, 0);
                list.Add(sprite);
                Add(sprite);
            }

            return list;
        }

        private void OnChangeMode(RoomCoreMode mode)
        {
            this.IceMode = mode == RoomCoreMode.Cold;

            string anim = this.IceMode ? "ice" : "hot";
            foreach (Sprite s in tiles)
                s.Play(anim);

            if (this.IceMode)
                this.idleSfx.Stop();
            else if (!this.idleSfx.Playing)
                this.idleSfx.Play("event:/env/local/09_core/conveyor_idle");
        }

        private bool IsRiding(ISourceJumpThruEntity jumpThru) =>
            SourceEntityCollision.CheckAt(this, (ISourceEntityCollider)jumpThru,
                this.Position + (this.Ceiling ? -Vector2.UnitY : Vector2.UnitY)) &&
            !SourceEntityCollision.Check(this, (ISourceEntityCollider)jumpThru);

        private bool IsRiding(SourceSolidEntityState solid)
        {
            if (SourceEntityCollision.CheckAt(this, solid,
                    this.Position + (this.Ceiling ? -Vector2.UnitY : Vector2.UnitY)) &&
                !SourceEntityCollision.Check(this, solid))
            {
                this.disableMode = (solid is CassetteBlockState or SwitchBlock)
                    ? DisableMode.ColorFade
                    : DisableMode.Disappear;
                return true;
            }

            return false;
        }

        private void OnShake(Vector2 amount) => this.imageOffset += amount;

        public override void Added(object scene)
        {
            base.Added(scene);

            RoomCoreMode mode = this.IceMode
                ? RoomCoreMode.Cold
                : RoomCoreMode.Hot;
            RoomCoreMode lvlMode = ((OriginalLevelSceneRoot)Scene).CoreMode;

            if (lvlMode is RoomCoreMode.Cold && !this.notCoreMode)
                mode = lvlMode;

            OnChangeMode(mode);
        }

        public override void Update()
        {
            RoomPlayerSession player =
                ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>(sourcePlayerRegistryKey);
            PositionSfx(player);

            if (((OriginalLevelSceneRoot)Scene).Transitioning)
                return;

            bool isUsed = false;
            base.Update();

            var matchGravity = this.Ceiling == runtime.IsPlayerInverted();

            if (matchGravity && player is not null && SourceEntityCollision.Check(this, player) && player.OnGround())
                isUsed = this.Ceiling ? player.Top >= this.Top : player.Bottom <= this.Bottom;

            PlayActivateSfx(this.IceMode || !isUsed);
        }

        private void PlayActivateSfx(bool end)
        {
            if (this.isPlaying ^ end)
                return;

            this.isPlaying = !end;
            if (end)
                this.activateSfx.Param("end", 1f);
            else
                this.activateSfx.Play("event:/game/09_core/conveyor_activate", "end", 0f);
        }

        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = this.Position;
            this.Position += this.imageOffset;
            base.Render(context);
            this.Position = position;
        }

        private void PositionSfx(RoomPlayerSession entity)
        {
            if (entity is null)
                return;

            this.idleSfx.Position =
                ((ISourceVectorEnvironment)engine).VectorArithmetic.ClosestPointOnLine(this.Position,
                    this.Position + new Vector2(this.Width, 0f), entity.Center) - this.Position;
            if (!this.Ceiling) this.idleSfx.Position.Y += 7;
            this.activateSfx.Position = this.idleSfx.Position;
            this.idleSfx.UpdateSfxPosition();
            this.activateSfx.UpdateSfxPosition();
        }
    }
}
