using System;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Platforms.CrushBlocks;
using Celeste.Game.Gameplay.Platforms.FallingBlocks;
using Celeste.Mods.VortexHelper.Entities.Switches;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.VortexHelper.Runtime
{
    public sealed class ColorSwitchInteractionHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly Func<OriginalCrushBlockState, Vector2> readDirection;
        private Hook falling, crush;

        public ColorSwitchInteractionHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            var field = typeof(OriginalCrushBlockState).GetField("crushDir",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingFieldException(typeof(OriginalCrushBlockState).FullName, "crushDir");
            var getter = new DynamicMethod("ReadCrushDirection", typeof(Vector2),
                new[] { typeof(OriginalCrushBlockState) }, typeof(ColorSwitchInteractionHooks), true);
            var il = getter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            readDirection =
                (Func<OriginalCrushBlockState, Vector2>)getter.CreateDelegate(
                    typeof(Func<OriginalCrushBlockState, Vector2>));
            try
            {
                falling = new Hook(
                    typeof(OriginalFallingBlockState).GetMethod("LandParticles",
                        BindingFlags.Instance | BindingFlags.NonPublic),
                    (Action<Action<OriginalFallingBlockState>, OriginalFallingBlockState>)LandParticles);
                crush = new Hook(typeof(OriginalCrushBlockState).GetMethod("Update"),
                    (Action<Action<OriginalCrushBlockState>, OriginalCrushBlockState>)CrushUpdate);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Own(SceneEntityState entity) => entity.Scene is ISourceEngineContextScene scene &&
                                                     ReferenceEquals(scene.EngineContext, engine);

        private void LandParticles(Action<OriginalFallingBlockState> original, OriginalFallingBlockState self)
        {
            original(self);
            if (Own(self)) SwitchHit(self, Vector2.UnitY);
        }

        private void CrushUpdate(Action<OriginalCrushBlockState> original, OriginalCrushBlockState self)
        {
            original(self);
            if (!Own(self)) return;
            var direction = readDirection(self);
            var history = self.Get<CrushColorSwitchHistory>();
            if (history == null)
            {
                history = new CrushColorSwitchHistory();
                self.OwnedComponents.Add(history);
            }

            if (history.PreviousDirection != Vector2.Zero && direction == Vector2.Zero)
                SwitchHit(self, history.PreviousDirection);
            history.PreviousDirection = direction;
        }

        private static void SwitchHit(SceneEntityState self, Vector2 direction)
        {
            foreach (ColorSwitch colorSwitch in ((ISourceScene)self.Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(ColorSwitch)))
                if (SourceEntityCollision.CheckAt(self, colorSwitch, self.Position + direction))
                    colorSwitch.Switch(direction);
        }

        public void Dispose()
        {
            crush?.Dispose();
            crush = null;
            falling?.Dispose();
            falling = null;
        }
    }
}
