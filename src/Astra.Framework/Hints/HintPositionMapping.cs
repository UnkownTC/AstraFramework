using System.Collections.Generic;

using Astra.Enums;

using HintServiceMeow.Core.Enum;

namespace Astra.Hints;
    /// <summary>
    /// 将 <see cref="HintPosition"/> 映射为 HintServiceMeow 的对齐方式与坐标。
    /// </summary>
    public static class HintPositionMapping
    {
        private static readonly IReadOnlyDictionary<HintPosition, (HintAlignment alignment, float y)> StaticMap = new Dictionary<HintPosition, (HintAlignment alignment, float y)>
        {
            { HintPosition.Center, (HintAlignment.Center, 650f) },
            { HintPosition.Left, (HintAlignment.Left, 530f) },
            { HintPosition.Right, (HintAlignment.Right, 530f) },
            { HintPosition.CenterTop, (HintAlignment.Center, 220f) },
            { HintPosition.CenterTopLeft, (HintAlignment.Left, 220f) },
            { HintPosition.CenterTopRight, (HintAlignment.Right, 220f) },
            { HintPosition.CenterBottom, (HintAlignment.Center, 860f) },
            { HintPosition.CenterBottomLeft, (HintAlignment.Left, 860f) },
            { HintPosition.CenterBottomRight, (HintAlignment.Right, 860f) },
            { HintPosition.Top, (HintAlignment.Center, 20f) },
            { HintPosition.TopLeft, (HintAlignment.Left, 20f) },
            { HintPosition.TopRight, (HintAlignment.Right, 20f) },
            { HintPosition.Bottom, (HintAlignment.Center, 1060f) },
            { HintPosition.BottomLeft, (HintAlignment.Left, 1060f) },
            { HintPosition.BottomRight, (HintAlignment.Right, 1060f) }
        };

        /// <summary>
        /// 获取静态提示的对齐方式与 Y 坐标；未知位置回退为居中（Center, 650）。
        /// </summary>
        /// <param name="position">提示位置。</param>
        public static (HintAlignment alignment, float y) GetForHint(HintPosition position)
        {
            return StaticMap.TryGetValue(position, out var value)
                ? value
                : (HintAlignment.Center, 650f);
        }

        /// <summary>
        /// 获取动态提示的 X/Y 坐标（根据对齐方式与文本宽度计算 X）。
        /// </summary>
        /// <param name="position">提示位置。</param>
        /// <param name="originXPos">起始 X 偏移。</param>
        /// <param name="textWidth">文本宽度。</param>
        public static (float x, float y) GetForDynamicHint(HintPosition position, float originXPos, float textWidth)
        {
            var pos = StaticMap.TryGetValue(position, out var value)
                ? value
                : (HintAlignment.Center, 650f);
            var offset = pos.Item1 switch
            {
                HintAlignment.Left => -1200f + textWidth / 2f,
                HintAlignment.Right => 1200f - textWidth / 2f,
                _ => 0f
            };

            return (originXPos + offset, pos.Item2);
        }
    }

