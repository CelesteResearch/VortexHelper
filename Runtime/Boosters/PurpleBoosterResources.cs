using System.Runtime.CompilerServices;
using System.Xml;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Content;
using Celeste.Graphics.Display.Colors;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.VortexHelper.Runtime.Boosters
{
    public sealed class PurpleBoosterResources
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, PurpleBoosterResources> owners = new();
        public static PurpleBoosterResources For(ISourceEngineContext engine) => owners.GetValue(engine, _ => new());
        public SpriteBank PurpleSprites { get; private set; }
        public SpriteBank LavenderSprites { get; private set; }
        public ParticleTypePresentation Burst { get; private set; }
        public ParticleTypePresentation Appear { get; private set; }
        public ParticleTypePresentation BurstExplode { get; private set; }
        public ParticleTypePresentation LavenderBurst { get; private set; }
        public ParticleTypePresentation LavenderBurstExplode { get; private set; }

        public static void LoadContent(ISourceEngineContext engine)
        {
            var resources = For(engine);
            resources.PurpleSprites = ReadBank(engine, "PurpleBoosterSprites");
            resources.LavenderSprites = ReadBank(engine, "LavenderBoosterSprites");
            var source = ((IOriginalGameplayProcess)engine).ParticleTypes.Booster;
            resources.Burst ??= new ParticleTypePresentation(source.P_Burst);
            resources.Appear ??= new ParticleTypePresentation(source.P_Appear);
            resources.BurstExplode ??= new ParticleTypePresentation(source.P_Burst);
            resources.LavenderBurst ??= new ParticleTypePresentation(source.P_Burst);
            resources.LavenderBurstExplode ??= new ParticleTypePresentation(source.P_Burst);
            resources.Burst.Color = SourceHexColor.HexToColor("8c2c95");
            resources.Appear.Color = SourceHexColor.HexToColor("b64acf");
            resources.BurstExplode.Color = resources.Burst.Color;
            resources.BurstExplode.SpeedMax = 250;
            resources.LavenderBurst.Color = SourceHexColor.HexToColor("6a38b0");
            resources.LavenderBurstExplode.Color = resources.LavenderBurst.Color;
            resources.LavenderBurstExplode.SpeedMax = 250;
        }

        private static SpriteBank ReadBank(ISourceEngineContext engine, string name)
        {
            var xml = new XmlDocument();
            using (var stream = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry
                   .Get("VortexHelper:/Graphics/" + name).Stream) xml.Load(stream);
            return new SpriteBank(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas, xml)
                { XMLPath = "Graphics/" + name + ".xml" };
        }
    }
}
