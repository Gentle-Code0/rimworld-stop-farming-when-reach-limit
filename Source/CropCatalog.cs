using System.Collections.Generic;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>一个可种植物及其标准收获产物的固定映射。</summary>
    public sealed class CropEntry
    {
        public ThingDef Plant;
        public ThingDef Product;
        public CropRule Rule;
        public bool Supported;
    }

    /// <summary>所有模组的 Def 加载完后构建一次目录；运行时不反复遍历 DefDatabase。</summary>
    public static class CropCatalog
    {
        public static readonly List<CropEntry> Entries = new List<CropEntry>();
        public static bool Ready { get; private set; }

        /// <summary>识别带有 sowTags 的植物，自动提取 harvestedThingDef，并检查原版统计资格。</summary>
        public static void Build()
        {
            Entries.Clear();
            FarmingMod.Settings.Reindex();
            List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                ThingDef plant = defs[i];
                if (plant.plant == null || plant.plant.sowTags == null || plant.plant.sowTags.Count == 0) continue;
                ThingDef product = plant.plant.harvestedThingDef;
                Entries.Add(new CropEntry
                {
                    Plant = plant,
                    Product = product,
                    Rule = FarmingMod.Settings.GetOrCreate(plant.defName),
                    Supported = product != null && product.CountAsResource
                });
            }
            Entries.Sort(CompareEntries);
            Ready = true;
        }

        /// <summary>按本地化植物名称排序设置目录；相同名称使用 defName 保证顺序稳定。</summary>
        private static int CompareEntries(CropEntry a, CropEntry b)
        {
            int result = string.Compare(a.Plant.label, b.Plant.label, System.StringComparison.CurrentCulture);
            return result != 0 ? result : string.CompareOrdinal(a.Plant.defName, b.Plant.defName);
        }
    }
}
