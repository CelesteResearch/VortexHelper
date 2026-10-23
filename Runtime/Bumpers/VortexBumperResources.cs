using System.Runtime.CompilerServices;
using System.Xml;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Content;
using Celeste.Graphics.Display.Colors;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.VortexHelper.Runtime.Bumpers
{
    public sealed class VortexBumperResources
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, VortexBumperResources> Owned = new();
        public SpriteBank Sprites { get; private set; }
        public ParticleTypePresentation GreenAmbience { get; private set; }
        public ParticleTypePresentation OrangeAmbience { get; private set; }
        public ParticleTypePresentation GreenLaunch { get; private set; }
        public ParticleTypePresentation OrangeLaunch { get; private set; }
        public static VortexBumperResources For(ISourceEngineContext engine) => Owned.GetValue(engine, _ => new());

        public static void LoadContent(ISourceEngineContext engine)
        {
            var resources = For(engine);
            var xml = new XmlDocument();
            using (var stream = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry
                   .Get("VortexHelper:/Graphics/VortexCustomBumperSprites").Stream) xml.Load(stream);
            resources.Sprites = new SpriteBank(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                    xml)
                { XMLPath = "Graphics/VortexCustomBumperSprites.xml" };
            var particles = ((IOriginalGameplayProcess)engine).ParticleTypes.Bumper;
            resources.GreenAmbience = new ParticleTypePresentation(particles.P_Ambience)
                { Color = SourceHexColor.HexToColor("5dcc47"), Color2 = SourceHexColor.HexToColor("c4ffc9") };
            resources.OrangeAmbience = new ParticleTypePresentation(particles.P_Ambience)
                { Color = SourceHexColor.HexToColor("cc9747"), Color2 = SourceHexColor.HexToColor("ffdfc4") };
            resources.GreenLaunch = new ParticleTypePresentation(particles.P_Launch)
                { Color = resources.GreenAmbience.Color, Color2 = resources.GreenAmbience.Color2 };
            resources.OrangeLaunch = new ParticleTypePresentation(particles.P_Launch)
                { Color = resources.OrangeAmbience.Color, Color2 = resources.OrangeAmbience.Color2 };
        }
    }
}
