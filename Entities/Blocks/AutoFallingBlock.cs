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
    [CustomEntity("VortexHelper/AutoFallingBlock")]
    [SceneTracked(false)]
    [RuntimeTypeAlias("Celeste.Mod.VortexHelper.Entities.AutoFallingBlock")]
    public partial class AutoFallingBlock : SourceSolidEntityState
    {
        private readonly TileGrid tiles;
        private readonly char TileType;
        private readonly int originalY;

        private readonly ISourceEngineContext engine;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;
        private IOriginalGameplayProcess Process => (IOriginalGameplayProcess)engine;
        private SceneEntityRegistry Registry => ((ISourceScene)Scene).SourceScene.Entities;

        public static AutoFallingBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) =>
            new(level.EngineContext, data, offset, level.SolidWorldFactory);

        public AutoFallingBlock(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : this(engine, data.Position + offset, data.Char("tiletype", '3'), data.Width, data.Height,
                worlds.Create(data.Position + offset, false))
        {
        }

        public AutoFallingBlock(ISourceEngineContext engine, Vector2 position, char tile, int width, int height,
            ISourceSolidWorld world) : base(position, width, height, false, world)
        {
            this.engine = engine;
            originalY = (int)position.Y;
            SourceRandomProcessState random = ((ISourceRandomProcessEnvironment)engine).RandomProcess;
            int newSeed = random.Random.Next();
            random.PushRandom(newSeed);
            Add(tiles = ((ISourceAutotilingResources)engine).ForegroundAutotiler
                .GenerateBox(tile, width / 8, height / 8).TileGrid);
            random.PopRandom();
            Add(new SimulationCoroutine(engine, Sequence()));
            Add(new LightOccludeState());
            Add(new TileInterceptorState(tiles, highPriority: false));
            TileType = tile;
            SurfaceSoundIndex = ((IOriginalTileSurfaceEnvironment)engine).SurfaceIndices.TileToIndex[tile];
        }

        public override void OnShake(Vector2 amount)
        {
            base.OnShake(amount);
            this.tiles.Position += amount;
        }
    }
}
