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
using System.Linq;
using Celeste.Core.Audio;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Entities.PlaybackBillboard;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Mods.VortexHelper.Entities.Blocks;

namespace Celeste.Mods.VortexHelper.Entities.Switches
{
    [CustomEntity("VortexHelper/ColorSwitch")]
    [SceneTracked]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.ColorSwitch")]
    public class ColorSwitch : SourceSolidEntityState
    {
        private uint seed;

        private readonly AtlasEntry[,] edges = new AtlasEntry[3, 3];

        private static readonly PackedColor defaultBackgroundColor = new PackedColor(25, 25, 25);
        private static readonly PackedColor defaultEdgeColor = new PackedColor(100, 100, 100);

        private PackedColor BackgroundColor = defaultBackgroundColor;
        private PackedColor EdgeColor = defaultEdgeColor;
        private PackedColor currentEdgeColor, currentBackgroundColor;

        private Vector2 scale = Vector2.One;
        private Vector2 scaleStrength = Vector2.One;

        private readonly SwitchBlockColor[] colors;
        private int nextColorIndex = 0;
        private readonly bool singleColor, random;

        public ColorSwitch(ISourceEngineContext engine, EntityData data, Vector2 offset, ISourceSolidWorld world)
            : this(engine, data.Position + offset, data.Width, data.Height,
                Celeste.Formats.Map.Entities.Attributes.EntityAttributeReader.Bool(data.Values, "blue"),
                Celeste.Formats.Map.Entities.Attributes.EntityAttributeReader.Bool(data.Values, "rose"),
                Celeste.Formats.Map.Entities.Attributes.EntityAttributeReader.Bool(data.Values, "orange"),
                Celeste.Formats.Map.Entities.Attributes.EntityAttributeReader.Bool(data.Values, "lime"),
                Celeste.Formats.Map.Entities.Attributes.EntityAttributeReader.Bool(data.Values, "random"), world)
        {
        }

        private readonly ISourceEngineContext engine;
        private readonly VortexColorRuntime runtime;
        private VortexColorRuntime Runtime => runtime;
        private AtlasData GameAtlas => ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas;
        private ISourceScalarArithmetic Scalar => ((ISourceScalarEnvironment)engine).ScalarArithmetic;
        private ISourceExtremaArithmetic Extrema => ((ISourceExtremaEnvironment)engine).ExtremaArithmetic;

        private PackedColor Lerp(PackedColor a, PackedColor b, float t) =>
            SourceColorInterpolation.Lerp(a, b, t, Scalar);

        public static SceneEntityState Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new ColorSwitch(level.EngineContext, data, offset,
            ((Celeste.Simulation.Gameplay.Platforms.Collision.ISourcePlatformCollisionScene)level).SolidWorldFactory
            .Create(data.Position + offset, true));

        public ColorSwitch(ISourceEngineContext engine, Vector2 position, int width, int height, bool blue, bool rose,
            bool orange, bool lime, bool random, ISourceSolidWorld world)
            : base(position, width, height, true, world)
        {
            this.engine = engine;
            runtime = VortexColorRuntime.For(engine);
            this.SurfaceSoundIndex = 7;

            // if all are false, then block is useless, so make it not useless.
            if (!blue && !rose && !orange && !lime)
                blue = rose = orange = lime = true;

            string block = "objects/VortexHelper/onoff/switch";
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                this.edges[i, j] = GameAtlas[block].GetSubtexture(i * 8, j * 8, 8, 8);

            this.random = random;

            bool[] colorBools = new bool[] { blue, rose, orange, lime };
            int colorArraySize = colorBools.Count(b => b);

            this.colors = new SwitchBlockColor[colorArraySize];
            this.singleColor = colorArraySize == 1;

            int arrIdx = 0;
            for (int i = 0; i < colorBools.Length; i++)
                if (colorBools[i])
                    this.colors[arrIdx++] = (SwitchBlockColor)i;

            NextColor(this.colors[this.nextColorIndex], true);

            OwnedComponents.Add(new LightOccludeState());
            OwnedComponents.Add(
                new SoundSourceComponentState(engine, "event:/game/01_forsaken_city/console_static_loop")
                {
                    Position = new Vector2(this.Width, this.Height) / 2f,
                });

            if (width > 32)
                this.scaleStrength.X = width / 32f;
            if (height > 32)
                this.scaleStrength.Y = height / 32f;

            PackedColor col = Runtime.IsActive(this.colors[this.nextColorIndex])
                ? defaultBackgroundColor
                : VortexColorRuntime.GetColor(this.colors[this.nextColorIndex]);
            SetEdgeColor(this.EdgeColor, this.EdgeColor);
            SetBackgroundColor(col, col);

            this.OnDashCollide = Dashed;
        }

        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = this.Position;
            this.Position += this.Shake;

            int x = (int)(this.Center.X + (this.X + 1 - this.Center.X) * this.scale.X);
            int y = (int)(this.Center.Y + (this.Y + 1 - this.Center.Y) * this.scale.Y);
            int rectW = (int)((this.Width - 2) * this.scale.X);
            int rectH = (int)((this.Height - 2) * this.scale.Y);
            var rect = new IntRect(x, y, rectW, rectH);

            PackedColor col = this.random
                ? Lerp(defaultBackgroundColor, PackedColor.White,
                    (float)(0.05f * Math.Sin(((ISourceScene)Scene).SourceScene.Clock.TimeActive * 5f)) + 0.05f)
                : this.BackgroundColor != defaultBackgroundColor
                    ? Lerp(this.currentBackgroundColor, PackedColor.Black, 0.2f)
                    : this.currentBackgroundColor;

            SourcePrimitiveDrawing.Rect((ISourceDrawScope)context, rect, col);

            for (int i = rect.Y; i < rect.Bottom; i += 2)
            {
                float scale = 0.05f +
                              (1f + (float)Math.Sin(i / 16f +
                                                    ((ISourceScene)Scene).SourceScene.Clock.TimeActive * 2f)) / 2f *
                              0.2f;
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, rect.X, i, rect.X + rect.Width, i,
                    PackedColor.White * 0.55f * scale, ((ISourceVectorEnvironment)engine).VectorArithmetic);
            }

            uint tempseed = this.seed;
            PlaybackBillboardState.DrawNoise(context, rect, ref tempseed, PackedColor.White * 0.05f, Extrema);

            int w = (int)(this.Width / 8f);
            int h = (int)(this.Height / 8f);

            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    int tx = (i != 0) ? ((i != w - 1f) ? 1 : 2) : 0;
                    int ty = (j != 0) ? ((j != h - 1f) ? 1 : 2) : 0;

                    if (tx == 1 && ty == 1)
                        continue;

                    Vector2 renderPos = new Vector2(i, j) * 8f + Vector2.One * 4f + this.Position;
                    renderPos = this.Center + (renderPos - Center) * scale;
                    SourceAtlasDrawing.DrawCentered(context, this, this.edges[tx, ty], renderPos, this.currentEdgeColor,
                        this.scale);
                }
            }

            base.Render(context);
            this.Position = position;
        }

        public override void Update()
        {
            base.Update();

            if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.1f, engine))
                this.seed++;

            float t = Extrema.Min(1f, 4f * engine.DeltaTime);
            this.currentEdgeColor = Lerp(this.currentEdgeColor, this.EdgeColor, t);
            this.currentBackgroundColor = Lerp(this.currentBackgroundColor, this.BackgroundColor, t);

            this.scale =
                ((ISourceVectorEnvironment)engine).VectorArithmetic.Approach(this.scale, Vector2.One,
                    engine.DeltaTime * 4f);
        }

        private void SetEdgeColor(PackedColor targetColor, PackedColor currentColor)
        {
            this.EdgeColor = targetColor;
            this.currentEdgeColor = currentColor;
        }

        private void SetBackgroundColor(PackedColor targetColor, PackedColor currentColor)
        {
            this.BackgroundColor = targetColor;
            this.currentBackgroundColor = currentColor;
        }

        private DashCollisionResult Dashed(RoomPlayerSession player, Vector2 direction)
        {
            if (!((IOriginalGameplayProcess)engine).SaveContext.CurrentSave.Assists.Invincible &&
                PlayerOnSpikes(player))
                return DashCollisionResult.NormalCollision;

            if (Runtime.IsActive(this.colors[this.nextColorIndex]))
                return DashCollisionResult.NormalCollision;

            // no switch, normal collision
            if (player.Machine.State == 5)
                player.Machine.State = 0;

            Switch(direction);
            return DashCollisionResult.Rebound;
        }

        public void Switch(Vector2 direction)
        {
            this.scale = new Vector2(
                1f + (Math.Abs(direction.Y) * 0.5f - Math.Abs(direction.X) * 0.5f) / this.scaleStrength.X,
                1f + (Math.Abs(direction.X) * 0.5f - Math.Abs(direction.Y) * 0.5f) / this.scaleStrength.Y
            );

            if (this.random)
                this.nextColorIndex = ((ISourceRandomEnvironment)engine).Random.Next(0, this.colors.Length);

            Runtime.Session.SessionSwitchBlockColor = this.colors[this.nextColorIndex];
            PackedColor col = VortexColorRuntime.GetColor(Runtime.Session.SessionSwitchBlockColor);

            UpdateColorSwitches(this.Scene, this.colors[this.nextColorIndex]);
            SetEdgeColor(defaultEdgeColor, col);
            this.currentBackgroundColor = PackedColor.White;

            ((ISourceSpatialAudio)engine).Play("event:/vortexHelperEvents/game/colorSwitch/hit", Center);
            if (SwitchBlock.RoomHasSwitchBlock(this.Scene, Runtime.Session.SessionSwitchBlockColor))
                PlaySwitchSound();

            ((ISourceRumbleEnvironment)engine).RumbleSession.Rumble((int)RumbleStrength.Strong, (int)RumbleLength.Long);
            ((OriginalLevelSceneRoot)Scene).DirectionalShake(direction, 0.25f);
            StartShaking(0.25f);

            ParticleTypePresentation p = ((IOriginalGameplayProcess)engine).ParticleTypes.LightningBreakerBox.P_Smash;
            p.Color = col;
            p.Color2 = Lerp(col, PackedColor.White, 0.5f);
            SmashParticles(Vector2Geometry.Perpendicular(direction), p);
            SmashParticles(-Vector2Geometry.Perpendicular(direction), p);
        }

        public void PlaySwitchSound() => PlaySwitchSound(engine);

        public static void PlaySwitchSound(ISourceEngineContext engine) =>
            ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/vortexHelperEvents/game/switchBlock/switch",
                "tone",
                VortexColorRuntime.GetSoundParam(VortexColorRuntime.For(engine).Session.SessionSwitchBlockColor));

        private bool PlayerOnSpikes(RoomPlayerSession player)
        {
            foreach (SpikesState spikes in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         "Celeste.Spikes"))
                if (SourceEntityCollision.Check(player, spikes))
                    return true;
            return false;
        }

        public static void UpdateColorSwitches(object scene, SwitchBlockColor color)
        {
            foreach (ColorSwitch colorSwitch in ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(ColorSwitch)))
                colorSwitch.NextColor(color, false);
        }

        private void NextColor(SwitchBlockColor colorNext, bool start)
        {
            if (colorNext == this.colors[this.nextColorIndex] && !this.singleColor)
            {
                if (!start)
                    this.nextColorIndex++;

                if (this.nextColorIndex > this.colors.Length - 1)
                    this.nextColorIndex = 0;

                if (Runtime.IsActive(this.colors[this.nextColorIndex]))
                    this.nextColorIndex++;
            }

            this.BackgroundColor = Runtime.IsActive(this.colors[this.nextColorIndex])
                ? defaultBackgroundColor
                : VortexColorRuntime.GetColor(this.colors[this.nextColorIndex]);
        }

        private void SmashParticles(Vector2 dir, ParticleTypePresentation smashParticle)
        {
            float direction;
            Vector2 position;
            Vector2 positionRange;
            int num;

            if (dir == Vector2.UnitX)
            {
                direction = 0f;
                position = this.CenterRight - Vector2.UnitX * 12f;
                positionRange = Vector2.UnitY * (this.Height - 6f) * 0.5f;
                num = (int)(this.Height / 8f) * 4;
            }
            else if (dir == -Vector2.UnitX)
            {
                direction = (float)Math.PI;
                position = this.CenterLeft + Vector2.UnitX * 12f;
                positionRange = Vector2.UnitY * (this.Height - 6f) * 0.5f;
                num = (int)(this.Height / 8f) * 4;
            }
            else if (dir == Vector2.UnitY)
            {
                direction = (float)Math.PI / 2f;
                position = this.BottomCenter - Vector2.UnitY * 12f;
                positionRange = Vector2.UnitX * (this.Width - 6f) * 0.5f;
                num = (int)(this.Width / 8f) * 4;
            }
            else
            {
                direction = -(float)Math.PI / 2f;
                position = this.TopCenter + Vector2.UnitY * 12f;
                positionRange = Vector2.UnitX * (this.Width - 6f) * 0.5f;
                num = (int)(this.Width / 8f) * 4;
            }

            num += 2;
            ((OriginalLevelSceneRoot)Scene).Particles.Emit(smashParticle, num, position, positionRange, direction);
        }
    }
}
