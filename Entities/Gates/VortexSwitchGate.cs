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
    [CustomEntity("VortexHelper/VortexSwitchGate")]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.VortexSwitchGate")]
    public partial class VortexSwitchGate : SourceSolidEntityState
    {
        [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.VortexSwitchGate+SwitchGateBehavior")]
        public enum SwitchGateBehavior
        {
            Crush,
            Shatter
        }

        private readonly IOriginalSwitchGateProcess process;
        private Vector2 node;
        private readonly bool persistent;
        public SwitchGateBehavior Behavior;
        private PackedColor inactiveColor = new PackedColor(95, 205, 228);
        private PackedColor activeColor = PackedColor.White;
        private PackedColor finishColor = new PackedColor(241, 65, 223);
        private Vector2 iconOffset;
        private readonly Sprite icon;
        private readonly WigglerComponent wiggler;
        private readonly AtlasEntry[,] nineSlice;
        private readonly SoundSourceComponentState openSfx;
        private readonly string debrisPath;
        private readonly float crushSpeed;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        public static VortexSwitchGate Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset, ((ISourcePlatformCollisionScene)level).SolidWorldFactory);

        public VortexSwitchGate(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : this((IOriginalSwitchGateProcess)engine, data.Position + offset, data.Width, data.Height,
                data.Nodes[0] + offset, data.Bool("persistent"), data.Attr("sprite", "block"),
                EntityAttributeReader.Enum(data.Values, "behavior", SwitchGateBehavior.Crush),
                data.Float("crushDuration"), worlds)
        {
        }

        public VortexSwitchGate(IOriginalSwitchGateProcess process, Vector2 position, int width, int height,
            Vector2 node, bool persistent, string spriteName, SwitchGateBehavior behavior, float crushSpeed,
            ISourceSolidWorldFactory worlds) : base(position, width, height, false, worlds.Create(position, false))
        {
            this.process = process;
            this.node = node;
            this.persistent = persistent;
            var extrema = ((ISourceExtremaEnvironment)process).ExtremaArithmetic;
            this.crushSpeed = extrema.Min(extrema.Max(crushSpeed, 0.5f), 2f);
            Behavior = behavior;
            debrisPath = spriteName switch
            {
                "mirror" => "debris/VortexHelper/disintegate/2",
                "temple" => "debris/VortexHelper/disintegate/3",
                "stars" => "debris/VortexHelper/disintegate/4",
                _ => "debris/VortexHelper/disintegate/1"
            };
            Add(icon = new Sprite(process, process.GameGraphics.GameAtlas, "objects/switchgate/icon")
            {
                Rate = 0f,
                Color = inactiveColor,
                Position = iconOffset = new Vector2(width / 2f, height / 2f)
            });
            icon.Add("spin", "", 0.1f, new Chooser<string>("spin"));
            icon.Play("spin");
            icon.CenterOrigin();
            Add(wiggler = process.WigglerPool.Create(0.5f, 4f, f => icon.Scale = Vector2.One * (1f + f)));
            nineSlice = SwitchGateRendering.CreateNineSlice(
                process.GameGraphics.GameAtlas["objects/switchgate/" + spriteName]);
            Add(openSfx = process.SoundSources.Create());
            Add(new LightOccludeState(0.5f));
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (OriginalSwitchComponent.CheckLevelFlag(Level))
            {
                if (Behavior == SwitchGateBehavior.Crush)
                {
                    MoveTo(node);
                    icon.Rate = 0f;
                    icon.SetAnimationFrame(0);
                    icon.Color = finishColor;
                }
                else RemoveSelf();
            }
            else
                Add(new SimulationCoroutine(process,
                    Behavior == SwitchGateBehavior.Crush ? CrushSequence(node) : ShatterSequence()));
        }

        public override void Render(ISourceRenderContext context)
        {
            SwitchGateRendering.RenderFrame(context, this, nineSlice);
            icon.Position = iconOffset + Shake;
            icon.DrawOutline(context);
            base.Render(context);
        }
    }
}
