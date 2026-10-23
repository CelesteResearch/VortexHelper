using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Gameplay.Hazards.Puffer;
using Celeste.Mods.VortexHelper.Entities.Barriers;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.VortexHelper.Runtime.Barriers
{
    public sealed class PufferBarrierHooks : IDisposable
    {
        private const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();
        private readonly Action<OriginalPufferState> explode;
        private readonly Action<OriginalPufferState> gotoGone;

        public PufferBarrierHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            explode = (Action<OriginalPufferState>)Delegate.CreateDelegate(typeof(Action<OriginalPufferState>),
                typeof(OriginalPufferState).GetMethod("Explode", Methods));
            gotoGone = (Action<OriginalPufferState>)Delegate.CreateDelegate(typeof(Action<OriginalPufferState>),
                typeof(OriginalPufferState).GetMethod("GotoGone", Methods));
            try
            {
                hooks.Add(new Hook(typeof(OriginalLevelLoaderScene).GetMethod("LoadingThread", Methods),
                    (Action<Action<OriginalLevelLoaderScene>, OriginalLevelLoaderScene>)LoadingThread));
                hooks.Add(new Hook(typeof(OriginalPufferState).GetMethod(nameof(OriginalPufferState.Update)),
                    (Action<Action<OriginalPufferState>, OriginalPufferState>)UpdatePuffer));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void LoadingThread(Action<OriginalLevelLoaderScene> original, OriginalLevelLoaderScene loader)
        {
            if (ReferenceEquals(loader.EngineContext, engine))
                loader.Level.AddEntity(new PufferBarrierRenderer(engine));
            original(loader);
        }

        private void UpdatePuffer(Action<OriginalPufferState> original, OriginalPufferState puffer)
        {
            original(puffer);
            if (!ReferenceEquals(puffer.ActorEngineContext, engine) || !puffer.Collidable) return;
            foreach (object entity in ((ISourceScene)puffer.Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(PufferBarrier)))
                ((PufferBarrier)entity).Collidable = true;
            PufferBarrier collided = null;
            foreach (object entity in ((ISourceScene)puffer.Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(PufferBarrier)))
                if (SourceEntityCollision.Check(puffer, (PufferBarrier)entity))
                {
                    collided = (PufferBarrier)entity;
                    break;
                }

            if (collided != null)
            {
                collided.OnTouchPuffer();
                explode(puffer);
                gotoGone(puffer);
            }

            foreach (object entity in ((ISourceScene)puffer.Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(PufferBarrier)))
                ((PufferBarrier)entity).Collidable = false;
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
