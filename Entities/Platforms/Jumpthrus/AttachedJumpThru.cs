using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Mods.VortexHelper.Entities.Blocks;
using Celeste.Mods.VortexHelper.Components.Platforms;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;

namespace Celeste.Mods.VortexHelper.Entities.Platforms.Jumpthrus
{
    [CustomEntity("VortexHelper/AttachedJumpThru")]
    [SceneTracked]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.AttachedJumpThru")]
    public class AttachedJumpThru : SourceJumpThruEntityState
    {
        public static AttachedJumpThru Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            EntityData data) => new(data, offset, level.SolidWorldFactory);

        private readonly int columns;

        private readonly int overrideSoundIndex = -1;
        private readonly string overrideTexture;

        private readonly SourceStaticMoverState staticMover;
        private SourceSolidEntityState attachedPlatform;

        private Vector2 imageOffset;

        public PackedColor EnabledColor = PackedColor.White;
        public PackedColor DisabledColor = PackedColor.White;
        public bool VisibleWhenDisabled;

        public AttachedJumpThru(EntityData data, Vector2 offset, ISourceSolidWorldFactory factory)
            : this(data.Position + offset, data.Width, data.Attr("texture", "default"), data.Int("surfaceIndex", -1),
                factory.CreateJumpThru(data.Position + offset, false))
        {
        }

        public AttachedJumpThru(Vector2 position, int width, string overrideTexture, int overrideSoundIndex,
            ISourceSolidWorld world)
            : base(position, width, false, world)
        {
            this.columns = width / 8;
            this.Depth = -60;

            this.overrideTexture = overrideTexture;
            this.overrideSoundIndex = overrideSoundIndex;

            this.staticMover = new SourceStaticMoverState
            {
                OnAttach = p => this.Depth = p.Depth + 1,
            };

            Add(new StaticMoverWithLiftSpeed
            {
                OnMove = OnMove,
                OnShake = OnShake,
                SolidChecker = IsRiding,
                OnEnable = OnEnable,
                OnDisable = OnDisable,
                OnSetLiftSpeed = OnSetLiftSpeed
            });
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);

            var areaData = (OriginalAreaData)((OriginalLevelSceneRoot)scene).AreaData;
            string jumpthru = areaData.Jumpthru;
            areaData.Jumpthru = jumpthru;

            if (!string.IsNullOrEmpty(this.overrideTexture) && !this.overrideTexture.Equals("default"))
                areaData.Jumpthru = this.overrideTexture;

            this.SurfaceSoundIndex = this.overrideSoundIndex > 0
                ? this.overrideSoundIndex
                : jumpthru.ToLower() switch
                {
                    "dream" => 32,
                    "temple" or "templeb" => 8,
                    "core" => 3,
                    _ => 5,
                };

            var mTexture =
                ((ISourceGameGraphicsEnvironment)SourceWorld.EngineContext).GameGraphics.GameAtlas[
                    "objects/jumpthru/" + areaData.Jumpthru];

            int w = mTexture.FrameWidth / 8;
            for (int i = 0; i < this.columns; i++)
            {
                int tx, ty;
                if (i == 0)
                {
                    tx = 0;
                    ty = SourceTrackedEntityCollision.CheckAt(((ISourceScene)Scene).SourceScene.Entities,
                        "Celeste.Solid",
                        this, this.Position + new Vector2(-1f, 0f))
                        ? 0
                        : 1;
                }
                else if (i == this.columns - 1)
                {
                    tx = w - 1;
                    ty = SourceTrackedEntityCollision.CheckAt(((ISourceScene)Scene).SourceScene.Entities,
                        "Celeste.Solid",
                        this, this.Position + new Vector2(1f, 0f))
                        ? 0
                        : 1;
                }
                else
                {
                    tx = 1 + SourceWorld.GameplayRandom.Next(w - 2);
                    ty = RandomSelection.Choose(SourceWorld.GameplayRandom, 0, 1);
                }

                Add(new SpriteImage(mTexture.GetSubtexture(tx * 8, ty * 8, 8, 8))
                {
                    Position = new Vector2(i * 8, 0)
                });
            }

            foreach (SourceStaticMoverState component in ((ISourceScene)scene).SourceScene.Components
                     .GetTrackedComponents(
                         "Celeste.StaticMover"))
            {
                if (component.IsRiding(this) && component.Platform is null)
                {
                    this.staticMovers.Add(component);
                    component.Platform = this;
                    component.OnAttach?.Invoke(this);
                }
            }
        }

        private bool IsRiding(SourceSolidEntityState solid)
        {
            if (!SourceEntityCollision.CheckAt(this, solid, this.Position + Vector2.UnitX) &&
                !SourceEntityCollision.CheckAt(this, solid, this.Position - Vector2.UnitX))
                return false;

            this.staticMover.Platform = this.attachedPlatform = solid;
            if (this.VisibleWhenDisabled =
                solid is Celeste.Game.Gameplay.Platforms.Cassette.CassetteBlockState or SwitchBlock)
                this.DisabledColor = PackedColor.Lerp(PackedColor.Black, PackedColor.White, .4f);
            return true;
        }

        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = this.Position;
            this.Position += this.imageOffset;
            base.Render(context);
            this.Position = position;
        }

        private void OnSetLiftSpeed(Vector2 liftSpeed) => this.LiftSpeed = liftSpeed;

        // Fix weird tp glitch with MoveBlocks
        private void OnMove(Vector2 amount)
        {
            float moveH = amount.X;
            float moveV = amount.Y;

            if (this.attachedPlatform is MoveBlockState moveBlock && moveBlock.Speed == 0f)
            {
                MoveHNaive(moveH);
                MoveVNaive(moveV);
            }
            else
            {
                MoveH(moveH, this.LiftSpeed.X);
                MoveV(moveV, this.LiftSpeed.Y);
            }
        }

        public override void OnShake(Vector2 amount)
        {
            this.imageOffset += amount;
            ShakeStaticMovers(amount);
        }

        private void OnDisable()
        {
            this.Active = this.Collidable = false;
            DisableStaticMovers();

            if (this.VisibleWhenDisabled)
                SetColor(this.DisabledColor);
            else
                this.Visible = false;
        }

        private void SetColor(PackedColor color)
        {
            foreach (SourceComponentState component in this.OwnedComponents)
                if (component is SpriteImage image)
                    image.Color = color;
        }

        private void OnEnable()
        {
            EnableStaticMovers();
            this.Active = this.Visible = this.Collidable = true;
            SetColor(PackedColor.White);
        }

        public override void OnStaticMoverTrigger(SourceStaticMoverState sm) => TriggerPlatform();

        public override void Update()
        {
            base.Update();

            RoomPlayerSession playerRider = GetPlayerRider();
            if (playerRider is not null && playerRider.Speed.Y >= 0f)
                TriggerPlatform();
        }

        private void TriggerPlatform() => this.attachedPlatform?.OnStaticMoverTrigger(this.staticMover);

        public override void MoveHExact(int move) => MoveHExactWithLiftSpeed(move);
    }
}
