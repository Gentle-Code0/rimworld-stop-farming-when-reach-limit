using UnityEngine;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>使用游戏自带种植区／收获图标和红叉直接叠绘，无须生成或分发贴图副本。</summary>
    [StaticConstructorOnStartup]
    internal static class PauseIcons
    {
        private static readonly Texture2D sow = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Growing");
        private static readonly Texture2D harvest = ContentFinder<Texture2D>.Get("UI/Designators/Harvest");
        private static readonly Texture2D cross = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
        internal const float Size = 28f;
        internal const float Gap = 4f;

        /// <summary>计算固定 UI 尺寸的并排布局；两个图标之间始终保留间隙，不随地图缩放重叠。</summary>
        internal static Rect Slot(Vector2 center, int slot, int count)
        {
            float total = count * Size + (count - 1) * Gap;
            return new Rect(center.x - total / 2f + slot * (Size + Gap), center.y - Size / 2f, Size, Size);
        }

        /// <summary>绘制操作图标并在右下角叠原版红叉，保留底层图案的辨识度。</summary>
        internal static void Draw(Rect rect, bool sowing)
        {
            Color oldColor = GUI.color;
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.08f, 0.70f));
            GUI.color = Color.white;
            GUI.DrawTexture(rect.ContractedBy(2f), sowing ? sow : harvest, ScaleMode.ScaleToFit);
            GUI.DrawTexture(new Rect(rect.xMax - 15f, rect.yMax - 15f, 15f, 15f), cross, ScaleMode.ScaleToFit);
            GUI.color = oldColor;
            // 只在鼠标确实悬停时创建翻译字符串，不为每个可见标记逐帧分配文本。
            if (Mouse.IsOver(rect)) TooltipHandler.TipRegion(rect,
                (sowing ? "SFRL_MenuPausedSowing" : "SFRL_MenuPausedHarvest").Translate());
        }
    }
}
