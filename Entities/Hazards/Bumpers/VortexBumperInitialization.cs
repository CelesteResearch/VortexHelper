using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.VortexHelper.Runtime.Bumpers;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.VortexHelper.Entities.Hazards.Bumpers
{
    internal sealed class VortexBumperInitialization
    {
        private readonly ISourceEngineContext engine;
        private readonly string style;
        private readonly string path;
        private readonly bool customColors;
        private readonly PackedColor particleColor;
        private readonly PackedColor particleColor2;
        public Vector2 Position { get; }
        public Vector2? Node { get; }
        public bool NotCoreMode { get; }
        public bool Wobble { get; }
        public bool TwoDashes { get; }
        public ParticleTypePresentation Ambience { get; private set; }
        public ParticleTypePresentation Launch { get; private set; }

        public VortexBumperInitialization(ISourceEngineContext engine, EntityData data, Vector2 offset)
        {
            this.engine = engine;
            Position = data.Position + offset;
            Node = data.FirstNodeNullable(offset);
            style = data.Attr("style", "Green");
            NotCoreMode = data.Bool("notCoreMore");
            Wobble = data.Bool("wobble", true);
            path = data.Attr("sprite").Trim().TrimEnd('/');
            customColors = data.Bool("useCustomParticleColors");
            particleColor = ReadHexColor(data, "particleColor1");
            particleColor2 = ReadHexColor(data, "particleColor2");
            TwoDashes = style != "Orange";
        }

        private static PackedColor ReadHexColor(EntityData data, string key)
        {
            if (data.Values.TryGetValue(key, out var value))
            {
                string text = value.ToString();
                if (text.Length == 6) return SourceHexColor.HexToColor(text);
            }

            return default;
        }

        public Sprite CreateSprite()
        {
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(path))
            {
                string name = style.ToLower();
                sprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                    path + "/");
                sprite.Add("on", name, .06f, "idle", 42, 43, 44);
                sprite.AddLoop("idle", name, .06f, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17,
                    18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33);
                sprite.Add("hit", name, .06f, "off", 34, 35, 36, 37, 38, 39, 40, 41, 42);
                sprite.AddLoop("off", name, .06f, 42);
                sprite.JustifyOrigin(.5f, .5f);
                sprite.Play("idle");
            }

            if (customColors)
            {
                var particles = ((IOriginalGameplayProcess)engine).ParticleTypes.Bumper;
                Ambience = new ParticleTypePresentation(particles.P_Ambience)
                    { Color = particleColor, Color2 = particleColor2 };
                Launch = new ParticleTypePresentation(particles.P_Launch)
                    { Color = Ambience.Color, Color2 = Ambience.Color2 };
            }

            var resources = VortexBumperResources.For(engine);
            sprite ??= resources.Sprites.Create(TwoDashes ? "greenBumper" : "orangeBumper");
            Ambience ??= TwoDashes ? resources.GreenAmbience : resources.OrangeAmbience;
            Launch ??= TwoDashes ? resources.GreenLaunch : resources.OrangeLaunch;
            return sprite;
        }
    }
}
