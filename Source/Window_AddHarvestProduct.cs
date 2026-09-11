using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>为使用完全自定义收获代码的作物手动关联产物；资源列表仅在打开窗口时建立。</summary>
    internal sealed class Window_AddHarvestProduct : Window
    {
        private readonly ThingDef plant;
        private readonly List<ThingDef> resources = new List<ThingDef>();
        private readonly List<ThingDef> filtered = new List<ThingDef>();
        private string search = "";
        private string previousSearch;
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(620f, 640f);

        /// <summary>缓存可由原版资源计数器统计的定义；不读取或扫描任何地图物品。</summary>
        internal Window_AddHarvestProduct(ThingDef plant)
        {
            this.plant = plant;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                if (def.CountAsResource) resources.Add(def);
            resources.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.CurrentCulture));
        }

        /// <summary>提供可搜索、虚拟化的产物选择列表；关联后默认忽略，等待玩家配置阈值。</summary>
        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(0f, 0f, rect.width - 30f, 30f), "SFRL_AddProductTitle".Translate(plant.LabelCap));
            search = Widgets.TextField(new Rect(0f, 36f, rect.width, 28f), search);
            if (previousSearch != search)
            {
                filtered.Clear();
                foreach (ThingDef def in resources)
                    if (string.IsNullOrEmpty(search)
                        || (def.label != null && def.label.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0)
                        || def.defName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) filtered.Add(def);
                previousSearch = search;
                scroll = Vector2.zero;
            }
            Rect outer = new Rect(0f, 72f, rect.width, Mathf.Max(40f, rect.height - 120f));
            Rect view = new Rect(0f, 0f, rect.width - 20f, filtered.Count * 40f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / 40f));
            int last = Mathf.Min(filtered.Count, Mathf.CeilToInt((scroll.y + outer.height) / 40f) + 1);
            for (int i = first; i < last; i++)
            {
                ThingDef product = filtered[i];
                Rect row = new Rect(0f, i * 40f, view.width, 38f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.DefIcon(new Rect(2f, row.y + 3f, 32f, 32f), product, drawPlaceholder: true);
                Widgets.Label(new Rect(42f, row.y + 6f, row.width - 44f, 28f), product.LabelCap);
                TooltipHandler.TipRegion(row, product.defName);
                if (Widgets.ButtonInvisible(row))
                {
                    CropCatalog.RegisterProduct(plant, product, manual: true);
                    Close();
                    break;
                }
            }
            Widgets.EndScrollView();
        }
    }
}

