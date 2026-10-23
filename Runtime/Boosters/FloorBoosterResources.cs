using System.Runtime.CompilerServices;
using System.Xml;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Graphics.Content;

namespace Celeste.Mods.VortexHelper.Runtime.Boosters
{
    public sealed class FloorBoosterResources
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, FloorBoosterResources> owners = new();
        public static FloorBoosterResources For(ISourceEngineContext engine) => owners.GetValue(engine, _ => new());
        public SpriteBank Sprites { get; private set; }

        public static void LoadContent(ISourceEngineContext engine)
        {
            var xml = new XmlDocument();
            using (var stream = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry
                   .Get("VortexHelper:/Graphics/FloorBoosterSprites").Stream) xml.Load(stream);
            For(engine).Sprites =
                new SpriteBank(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas, xml)
                    { XMLPath = "Graphics/FloorBoosterSprites.xml" };
        }
    }
}
