using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Rendering;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Interaction.Springs;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Geometry.Tiling;
using Celeste.Mods.VortexHelper.Configuration;
using Celeste.Mods.VortexHelper.Entities.Boosters;
using Celeste.Mods.VortexHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Hazards.Spikes;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Presentation.Lighting;

namespace Celeste.Mods.VortexHelper.Entities.Blocks
{
    [CustomEntity("VortexHelper/SwitchBlock")]
    [SceneTracked]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.SwitchBlock")]
    public class SwitchBlock : SourceSolidEntityState
    {
        private static readonly string sourcePlayerRegistryKey =
            typeof(global::Celeste.Simulation.RoomPlayerSession).FullName;

        private bool Activated;
        private readonly SwitchBlockColor switchBlockColor;
        private readonly int index;
        private readonly PackedColor color;

        private readonly LightOccludeState occluder;
        private WigglerComponent wiggler;
        private Vector2 wigglerScaler;

        private List<SwitchBlock> group;
        private bool groupLeader;
        private Vector2 groupOrigin;

        private readonly List<SpriteImage> pressed = new();
        private readonly List<SpriteImage> solid = new();
        private readonly List<SpriteImage> all = new();


        private readonly ISourceEngineContext engine;
        private readonly VortexColorRuntime runtime;
        private VortexColorRuntime Runtime => runtime;
        private AtlasData GameAtlas => ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas;

        public static SceneEntityState
            Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
                OriginalLevelEntityData data) => new SwitchBlock(level.EngineContext, data.Position + offset,
            data.Width, data.Height,
            Celeste.Formats.Map.Entities.Attributes.EntityAttributeReader.Int(data.Values, "index", 0),
            ((ISourcePlatformCollisionScene)level).SolidWorldFactory.Create(data.Position + offset, true));

        public SwitchBlock(ISourceEngineContext engine, Vector2 position, int width, int height, int index,
            ISourceSolidWorld world)
            : base(position, width, height, true, world)
        {
            this.engine = engine;
            runtime = VortexColorRuntime.For(engine);
            this.SurfaceSoundIndex = 35;
            this.index = index;

            this.switchBlockColor = this.index switch
            {
                1 => SwitchBlockColor.Rose,
                2 => SwitchBlockColor.Orange,
                3 => SwitchBlockColor.Lime,
                _ => SwitchBlockColor.Blue,
            };

            this.color = VortexColorRuntime.GetColor(this.switchBlockColor);
            this.Activated = this.Collidable = Runtime.IsActive(this.switchBlockColor);

            OwnedComponents.Add(this.occluder = new LightOccludeState());
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            PackedColor color = new PackedColor(102, 125, 165);
            var disabledColor = new PackedColor(color.R / 255f * (this.color.R / 255f),
                color.G / 255f * (this.color.G / 255f), color.B / 255f * (this.color.B / 255f), 1f);

            foreach (SourceStaticMoverState staticMover in this.staticMovers)
            {
                switch (staticMover.Entity)
                {
                    case SpikesState e:
                    {
                        e.EnabledColor = this.color;
                        e.DisabledColor = disabledColor;
                        e.VisibleWhenDisabled = true;
                        e.SetSpikeColor(this.color);
                        break;
                    }

                    case OriginalSpringState e:
                    {
                        e.DisabledColor = disabledColor;
                        e.VisibleWhenDisabled = true;
                        break;
                    }

                    case FloorBooster e:
                    {
                        e.EnabledColor = this.color;
                        e.DisabledColor = disabledColor;
                        e.SetColor(this.color);
                        break;
                    }

                    default:
                        break;
                }
            }

            if (this.group is null)
            {
                this.groupLeader = true;
                this.group = new List<SwitchBlock>
                {
                    this
                };
                FindInGroup(this);

                float groupLeft = float.MaxValue;
                float groupRight = float.MinValue;
                float groupTop = float.MaxValue;
                float groupBottom = float.MinValue;
                foreach (SwitchBlock item in this.group)
                {
                    if (item.Left < groupLeft)
                        groupLeft = item.Left;
                    if (item.Right > groupRight)
                        groupRight = item.Right;
                    if (item.Bottom > groupBottom)
                        groupBottom = item.Bottom;
                    if (item.Top < groupTop)
                        groupTop = item.Top;
                }

                this.groupOrigin = new Vector2((int)(groupLeft + (groupRight - groupLeft) / 2f), (int)groupBottom);
                this.wigglerScaler = new Vector2(ScalarMath.ClampedMap(groupRight - groupLeft, 32f, 96f, 1f, 0.2f),
                    ScalarMath.ClampedMap(groupBottom - groupTop, 32f, 96f, 1f, 0.2f));
                OwnedComponents.Add(this.wiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(0.3f, 3f));
                foreach (SwitchBlock block in this.group)
                {
                    block.wiggler = this.wiggler;
                    block.wigglerScaler = this.wigglerScaler;
                    block.groupOrigin = this.groupOrigin;
                }
            }

            foreach (SourceStaticMoverState staticMover2 in this.staticMovers)
                (staticMover2.Entity as SpikesState)?.SetOrigins(this.groupOrigin);

            string idx = this.index switch
            {
                1 => "red",
                2 => "orange",
                3 => "green",
                _ => "blue",
            };

            ConnectedBlockAutotiling.Build(Left, Right, Top, Bottom, CheckForSame,
                (x, y, tx, ty) => SetImage(x, y, tx, ty, idx));

            if (!this.Activated)
                DisableStaticMovers();

            UpdateVisualState();
        }

        private void FindInGroup(SwitchBlock block)
        {
            foreach (SwitchBlock entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(SwitchBlock)))
            {
                if (entity != this && entity != block && entity.index == this.index &&
                    (SourceEntityGeometryCollision.Rectangle(entity,
                         new IntRect((int)block.X - 1, (int)block.Y, (int)block.Width + 2, (int)block.Height)) ||
                     SourceEntityGeometryCollision.Rectangle(entity,
                         new IntRect((int)block.X, (int)block.Y - 1, (int)block.Width, (int)block.Height + 2))) &&
                    !this.group.Contains(entity))
                {
                    this.group.Add(entity);
                    FindInGroup(entity);
                    entity.group = this.group;
                }
            }
        }

        private bool CheckForSame(float x, float y)
        {
            foreach (SwitchBlock entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(SwitchBlock)))
                if (entity.index == this.index &&
                    SourceEntityGeometryCollision.Rectangle(entity.Collider, new IntRect((int)x, (int)y, 8, 8)))
                    return true;
            return false;
        }

        private void SetImage(float x, float y, int tx, int ty, string idx)
        {
            this.pressed.Add(CreateImage(x, y, tx, ty, GameAtlas["objects/VortexHelper/onoff/outline_" + idx]));
            this.solid.Add(CreateImage(x, y, tx, ty, GameAtlas["objects/VortexHelper/onoff/solid"]));
        }

        private SpriteImage CreateImage(float x, float y, int tx, int ty, AtlasEntry tex)
        {
            var value = new Vector2(x - this.X, y - this.Y);
            Vector2 vector = this.groupOrigin - this.Position;

            var image = new SpriteImage(tex.GetSubtexture(tx * 8, ty * 8, 8, 8))
            {
                Origin = vector - value,
                Position = vector,
                Color = color
            };

            OwnedComponents.Add(image);
            this.all.Add(image);
            return image;
        }

        private void UpdateVisualState()
        {
            this.Depth = this.Collidable ? -10 : 9880;

            foreach (SourceStaticMoverState staticMover in this.staticMovers)
                ((SceneEntityState)staticMover.Entity).Depth = this.Depth + 1;

            this.occluder.Visible = this.Collidable;

            foreach (SpriteImage image in this.solid)
                image.Visible = this.Collidable;
            foreach (SpriteImage image in this.pressed)
                image.Visible = !this.Collidable;

            if (this.groupLeader)
            {
                var scale = new Vector2(1f + this.wiggler.Value * 0.05f * this.wigglerScaler.X,
                    1f + this.wiggler.Value * 0.15f * this.wigglerScaler.Y);
                foreach (SwitchBlock item3 in this.group)
                {
                    foreach (SpriteImage img in item3.all)
                        img.Scale = scale;

                    foreach (SourceStaticMoverState staticMover2 in item3.staticMovers)
                        if (staticMover2.Entity is SpikesState spikes)
                            foreach (SourceComponentState component in spikes.OwnedComponents)
                                if (component is SpriteImage image)
                                    image.Scale = scale;
                }
            }
        }

        private bool CheckPlayerSafe()
        {
            RoomPlayerSession player =
                ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>(sourcePlayerRegistryKey);
            if (player is null)
                return true;

            foreach (SwitchBlock block in this.group)
                if (SourceEntityCollision.Check(block, player))
                    return false;

            return true;
        }

        public override void Update()
        {
            base.Update();
            this.Activated = Runtime.IsActive(this.switchBlockColor);

            if (this.groupLeader && this.Activated && !this.Collidable)
            {
                bool isPlayerSafe = CheckPlayerSafe();
                if (isPlayerSafe)
                {
                    foreach (SwitchBlock item2 in this.group)
                    {
                        item2.Collidable = true;
                        item2.EnableStaticMovers();
                    }

                    this.wiggler.Start();
                }
            }
            else if (!this.Activated && this.Collidable)
            {
                this.Collidable = false;
                DisableStaticMovers();
            }

            UpdateVisualState();
        }

        public static bool RoomHasSwitchBlock(object scene, SwitchBlockColor targetColor)
        {
            foreach (SwitchBlock switchBlock in ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(SwitchBlock)))
                if (switchBlock.switchBlockColor == targetColor)
                    return true;
            return false;
        }
    }
}
