using System.Collections.Generic;
using Verse;

namespace StopFarmingItsEnough
{
    /// <summary>一个作物＋一种收获产物的配置行；同种作物可以有多行。</summary>
    public sealed class CropEntry
    {
        public ThingDef Plant;
        public ThingDef Product;
        public CropRule Rule;
        public bool Supported;
    }

    /// <summary>供其他模组通过 modExtensions 显式声明额外产物，避免等待首次收获。</summary>
    public sealed class HarvestProductsExtension : DefModExtension
    {
        public List<ThingDef> products;
    }

    /// <summary>启动时建立定义目录；仅在首次发现额外产物时增量更新，不扫描库存。</summary>
    public static class CropCatalog
    {
        public static readonly List<CropEntry> Entries = new List<CropEntry>();
        public static readonly List<ThingDef> Plants = new List<ThingDef>();
        private static readonly HashSet<ThingDef> knownPlants = new HashSet<ThingDef>();
        private static readonly HashSet<ThingDef> withProducts = new HashSet<ThingDef>();
        private static readonly Dictionary<string, CropEntry> pairs = new Dictionary<string, CropEntry>();
        public static bool Ready { get; private set; }
        public static int Revision { get; private set; }

        /// <summary>发现可种植物、标准产物、XML 扩展以及已保存的额外产物映射。</summary>
        public static void Build()
        {
            Ready = false;
            Entries.Clear();
            Plants.Clear();
            knownPlants.Clear();
            withProducts.Clear();
            pairs.Clear();
            FarmingMod.Settings.Reindex();
            foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (plant.plant?.sowTags == null || plant.plant.sowTags.Count == 0) continue;
                Plants.Add(plant);
                knownPlants.Add(plant);
                AddPair(plant, plant.plant.harvestedThingDef);
                var extension = plant.GetModExtension<HarvestProductsExtension>();
                if (extension?.products != null)
                    foreach (ThingDef product in extension.products) AddPair(plant, product);
            }
            // 使用快照，避免创建缺失规则时修改正在遍历的集合。
            foreach (CropRule rule in FarmingMod.Settings.Rules.ToArray())
            {
                if (string.IsNullOrEmpty(rule.ProductDefName)) continue;
                ThingDef plant = DefDatabase<ThingDef>.GetNamedSilentFail(rule.PlantDefName);
                ThingDef product = DefDatabase<ThingDef>.GetNamedSilentFail(rule.ProductDefName);
                if (plant != null && knownPlants.Contains(plant)) AddPair(plant, product, rule.Source);
            }
            foreach (ThingDef plant in Plants)
                if (!withProducts.Contains(plant)) Entries.Add(new CropEntry { Plant = plant });
            Entries.Sort(CompareEntries);
            Ready = true;
            Revision++;
        }

        /// <summary>实际收获或手工关联时登记新产物；重复事件只进行哈希查询。</summary>
        public static void RegisterProduct(ThingDef plant, ThingDef product, bool manual = false)
        {
            if (!Ready || plant == null || !knownPlants.Contains(plant) || !AddPair(plant, product, manual ? ProductLinkSource.Manual : ProductLinkSource.Automatic)) return;
            Entries.RemoveAll(entry => entry.Plant == plant && entry.Product == null);
            Entries.Sort(CompareEntries);
            Revision++;
        }

        /// <summary>创建唯一的作物＋产物映射，新副产物默认忽略；不符合原版统计口径的产物仅显示。</summary>
        private static bool AddPair(ThingDef plant, ThingDef product, ProductLinkSource source = ProductLinkSource.Automatic)
        {
            if (product == null) return false;
            string key = FarmingSettings.Key(plant.defName, product.defName);
            CropEntry existing;
            if (pairs.TryGetValue(key, out existing))
            {
                // 自动证据优先；点击“＋”不能把自动或旧版未知关联变成可删除项。
                if (source == ProductLinkSource.Automatic && existing.Rule.Source != source)
                {
                    existing.Rule.Source = source;
                    FarmingMod.Settings.Changed();
                    Revision++;
                }
                return false;
            }
            withProducts.Add(plant);
            var rule = FarmingMod.Settings.GetOrCreate(plant.defName, product.defName, product == plant.plant.harvestedThingDef);
            rule.Source = source;
            if (source == ProductLinkSource.Manual && string.IsNullOrEmpty(rule.StateId))
                rule.StateId = System.Guid.NewGuid().ToString("N");
            var entry = new CropEntry
            {
                Plant = plant, Product = product, Rule = rule,
                Supported = product.CountAsResource
            };
            pairs.Add(key, entry);
            Entries.Add(entry);
            return true;
        }

        /// <summary>检查实际目录身份和来源，防止旧界面引用或构造的条目删除自动关联。</summary>
        public static bool CanRemoveManual(CropEntry entry)
        {
            if (entry?.Rule == null || entry.Product == null || entry.Rule.Source != ProductLinkSource.Manual) return false;
            CropEntry current;
            return pairs.TryGetValue(FarmingSettings.Key(entry.Plant.defName, entry.Product.defName), out current)
                && ReferenceEquals(current, entry);
        }

        /// <summary>移除手工关联及其设置；删除最后一个产物时恢复作物占位行，允许再次添加。</summary>
        public static bool RemoveManualProduct(CropEntry entry)
        {
            if (!CanRemoveManual(entry) || !FarmingMod.Settings.RemoveManual(entry.Rule)) return false;
            pairs.Remove(FarmingSettings.Key(entry.Plant.defName, entry.Product.defName));
            Entries.Remove(entry);
            if (!Entries.Exists(other => other.Plant == entry.Plant))
            {
                withProducts.Remove(entry.Plant);
                Entries.Add(new CropEntry { Plant = entry.Plant });
            }
            Entries.Sort(CompareEntries);
            Revision++;
            return true;
        }
        /// <summary>按作物本地化名称分组，组内主产物优先，其余按产物名称稳定排序。</summary>
        private static int CompareEntries(CropEntry a, CropEntry b)
        {
            int result = string.Compare(a.Plant.label, b.Plant.label, System.StringComparison.CurrentCulture);
            if (result == 0) result = string.CompareOrdinal(a.Plant.defName, b.Plant.defName);
            if (result != 0) return result;
            bool primaryA = a.Product == a.Plant.plant.harvestedThingDef;
            bool primaryB = b.Product == b.Plant.plant.harvestedThingDef;
            if (primaryA != primaryB) return primaryA ? -1 : 1;
            result = string.Compare(a.Product?.label, b.Product?.label, System.StringComparison.CurrentCulture);
            return result != 0 ? result : string.CompareOrdinal(a.Product?.defName, b.Product?.defName);
        }
    }
}


