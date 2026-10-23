using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Collections;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Audio.Surfaces;
using Celeste.Game.Content.Maps;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Tiles;
using Celeste.Graphics.Content.Tiles.Autotiling;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Tiles;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Core.Randomness;
using Celeste.Mods.VortexHelper.Entities.Switches;

namespace Celeste.Mods.VortexHelper.Entities.Blocks
{
    public partial class AutoFallingBlock
    {
        private void LandParticles()
        {
            for (int i = 2; i <= this.Width; i += 4)
            {
                if (SourceSceneCollision.CheckPoint(Registry, "Celeste.Solid", this.BottomLeft + new Vector2(i, 3f)))
                {
                    Level.ParticlesFG.Emit(Process.ParticleTypes.FallingBlock.P_FallDustA, 1,
                        new Vector2(this.X + i, this.Bottom), Vector2.One * 4f, -(float)Math.PI / 2f);
                    float direction = (!(i < this.Width / 2f)) ? 0f : ((float)Math.PI);
                    Level.ParticlesFG.Emit(Process.ParticleTypes.FallingBlock.P_LandDust, 1,
                        new Vector2(this.X + i, this.Bottom), Vector2.One * 4f, direction);
                }
            }
        }

        private void ImpactSfx()
        {
            Process.AudioSession.Play(this.TileType switch
            {
                '3' => "event:/game/01_forsaken_city/fallblock_ice_impact",
                '9' => "event:/game/03_resort/fallblock_wood_impact",
                'g' => "event:/game/06_reflection/fallblock_boss_impact",
                _ => "event:/game/general/fallblock_impact",
            });
        }
    }
}
