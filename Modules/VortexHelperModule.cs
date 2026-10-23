using System;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Mods.VortexHelper.Configuration;
using Celeste.Mods.VortexHelper.Runtime;
using Celeste.Mods.VortexHelper.Runtime.Barriers;
using Celeste.Mods.VortexHelper.Runtime.Puffers;
using Celeste.Mods.VortexHelper.Runtime.Particles;
using Celeste.Mods.VortexHelper.Runtime.Boosters;

namespace Celeste.Mods.VortexHelper.Modules
{
    public sealed class VortexHelperModule : SourceEverestModule
    {
        public VortexHelperModule(ISourceEngineContext engine) : base(engine)
        {
        }

        public ISourceEngineContext Engine => SourceEngine;
        public override Type SessionType => typeof(VortexHelperSession);
        private ColorSwitchInteractionHooks interactions;
        private PufferBarrierHooks pufferBarriers;
        private BowlPufferRuntime bowlPuffers;
        private PurpleBoosterRuntime purpleBoosters;
        private FloorBoosterHooks floorBoosters;

        public override void Load()
        {
            VortexColorRuntime.Register(this);
            try
            {
                interactions = new ColorSwitchInteractionHooks(SourceEngine);
                purpleBoosters = PurpleBoosterRuntime.For(SourceEngine);
                purpleBoosters.Load(this);
                floorBoosters = new FloorBoosterHooks(SourceEngine);
                bowlPuffers = BowlPufferRuntime.Load(SourceEngine);
                pufferBarriers = new PufferBarrierHooks(SourceEngine);
            }
            catch
            {
                pufferBarriers?.Dispose();
                pufferBarriers = null;
                if (bowlPuffers != null) BowlPufferRuntime.Unload(SourceEngine);
                bowlPuffers = null;
                floorBoosters?.Dispose();
                floorBoosters = null;
                purpleBoosters?.Dispose();
                purpleBoosters = null;
                interactions?.Dispose();
                interactions = null;
                VortexColorRuntime.Unregister(SourceEngine);
                throw;
            }
        }

        public override void LoadContent(bool firstLoad)
        {
            BowlPufferRuntime.LoadContent(SourceEngine);
            PurpleBoosterResources.LoadContent(SourceEngine);
            FloorBoosterResources.LoadContent(SourceEngine);
            Celeste.Mods.VortexHelper.Runtime.Bumpers.VortexBumperResources.LoadContent(SourceEngine);
            BubbleWrapParticles.Initialize(SourceEngine);
        }

        public override void Unload()
        {
            pufferBarriers?.Dispose();
            pufferBarriers = null;
            if (bowlPuffers != null) BowlPufferRuntime.Unload(SourceEngine);
            bowlPuffers = null;
            floorBoosters?.Dispose();
            floorBoosters = null;
            purpleBoosters?.Dispose();
            purpleBoosters = null;
            interactions?.Dispose();
            interactions = null;
            VortexColorRuntime.Unregister(SourceEngine);
        }
    }
}
