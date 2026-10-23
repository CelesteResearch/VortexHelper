using System;
using System.Numerics;
using Celeste.Core.Rendering;
using Celeste.Core.Display;
using Celeste.Graphics.Geometry.Tiling;
using Celeste.Graphics.Rendering;

namespace Celeste.Mods.VortexHelper.Entities.Blocks
{
    public partial class BubbleWrapBlock
    {
        public override void Render(ISourceRenderContext context)
        {
            var drawOffset = Vector2.One * 4f;
            float w = Collider.Width / 8f - 1f, h = Collider.Height / 8f - 1f;
            for (int i = 0; i <= w; i++)
            for (int j = 0; j <= h; j++)
            {
                int tx = i < w ? Math.Min(i, 1) : 2, ty = j < h ? Math.Min(j, 1) : 2;
                var pos = Position + new Vector2(i * 8, j * 8);
                var newPos = Center + (pos - Center) * wobbleScale + Shake;
                SourceAtlasDrawing.DrawCentered(context, this, nineSlice[tx, ty, state == States.Idle ? 0 : 1],
                    newPos + drawOffset, PackedColor.White, wobbleScale);
            }

            if (state == States.Gone)
                SourcePrimitiveDrawing.HollowRect(context, this, (int)X - (int)rectEffectInflate,
                    (int)Y - (int)rectEffectInflate, (int)Width + 2 * (int)rectEffectInflate,
                    (int)Height + 2 * (int)rectEffectInflate, PackedColor.White * ((3f - rectEffectInflate) / 3f));
            base.Render(context);
        }
    }
}
