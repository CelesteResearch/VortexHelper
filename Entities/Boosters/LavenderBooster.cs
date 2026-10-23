using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Entities.Interaction.Boosters;
using Celeste.Mods.VortexHelper.Runtime.Boosters;

namespace Celeste.Mods.VortexHelper.Entities.Boosters
{
    [CustomEntity("VortexHelper/LavenderBooster")]
    [EverestTrackedAs(typeof(OriginalBoosterState))]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.LavenderBooster")]
    public class LavenderBooster : OriginalBoosterState
    {
        internal readonly bool QoL;
        internal readonly bool LegacyGravityHelper;

        public LavenderBooster(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(engine,
            data.Position + offset, false)
        {
            QoL = data.Bool("QoL", false);
            LegacyGravityHelper = data.Bool("legacyGravityHelper", false);
            var resources = PurpleBoosterResources.For(engine);
            Remove(BoosterSprite);
            BoosterSprite = resources.LavenderSprites.Create("lavenderBooster");
            Add(BoosterSprite);
            BoosterParticleType = resources.LavenderBurst;
        }
    }
}
