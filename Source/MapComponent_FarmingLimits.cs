using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>逐地图保存滞回状态；热路径只查字典，库存检查绝不遍历地格、田地或物品堆。</summary>
    public sealed partial class MapComponent_FarmingLimits : MapComponent
    {
        public const int CheckInterval = 600;
        private static readonly ConditionalWeakTable<Map, MapComponent_FarmingLimits> cache =
            new ConditionalWeakTable<Map, MapComponent_FarmingLimits>();
        private Dictionary<string, bool> savedPaused = new Dictionary<string, bool>();
        private List<string> saveKeys;
        private List<bool> saveValues;
        private readonly Dictionary<ThingDef, List<RuntimeRule>> byPlant = new Dictionary<ThingDef, List<RuntimeRule>>();
        private readonly List<RuntimeRule> active = new List<RuntimeRule>();
        private readonly List<ThingDef> products = new List<ThingDef>();
        private int[] stock = new int[0];
        private bool[] available = new bool[0];
        private int builtRevision = -1;
        private bool initialized;

        /// <summary>某地图上某植物的运行状态；产物索引用于合并重复库存读取。</summary>
        private sealed class RuntimeRule
        {
            public CropEntry Entry;
            public string Key;
            public int ProductIndex;
            public bool Paused;
            public bool CountAvailable = true;
        }

        /// <summary>由 RimWorld 自动创建组件；弱引用索引不会把已卸载地图永久留在内存。</summary>
        public MapComponent_FarmingLimits(Map map) : base(map)
        {
            cache.Remove(map);
            cache.Add(map, this);
        }

        /// <summary>常数时间取得地图组件，避免每次工作检查都遍历 Map.components。</summary>
        public static MapComponent_FarmingLimits For(Map map)
        {
            MapComponent_FarmingLimits component;
            return map != null && cache.TryGetValue(map, out component) ? component : null;
        }

        /// <summary>保存滞回记忆，保证读档后库存位于阈值区间内时不意外恢复工作。</summary>
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref savedPaused, "sfrlPausedByPlant", LookMode.Value, LookMode.Value,
                ref saveKeys, ref saveValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (savedPaused == null) savedPaused = new Dictionary<string, bool>();
                initialized = false;
            }
        }

        /// <summary>读图后恢复规则和保存状态，不强制刷新或读取原版库存。</summary>
        public override void FinalizeInit()
        {
            if (CropCatalog.Ready) Rebuild();
        }

        /// <summary>每 tick 只检查开关及整数周期；固定每 600 ticks 读取原版缓存一次。</summary>
        public override void MapComponentTick()
        {
            FarmingSettings settings = FarmingMod.Settings;
            if (!CropCatalog.Ready || settings == null) return;
            if (!initialized) Rebuild();
            if (!settings.Enabled || (!settings.ControlSowing && !settings.ControlHarvest)) return;
            if (Find.TickManager.TicksGame % CheckInterval != 0) return;
            if (builtRevision != settings.Revision) Rebuild();
            CheckStocks();
        }

        /// <summary>按植物建立状态、按产物去重。仅在首次初始化或设置变更后的检查点分配内存。</summary>
        private void Rebuild()
        {
            byPlant.Clear();
            active.Clear();
            products.Clear();
            var productIndices = new Dictionary<ThingDef, int>();
            foreach (CropEntry entry in CropCatalog.Entries)
            {
                CropRule rule = entry.Rule;
                if (!entry.Supported || rule.Ignore || !Hysteresis.Valid(rule.Lower, rule.Upper)) continue;
                int index;
                if (!productIndices.TryGetValue(entry.Product, out index))
                {
                    index = products.Count;
                    productIndices.Add(entry.Product, index);
                    products.Add(entry.Product);
                }
                bool paused;
                string key = rule.StateKey;
                if (!savedPaused.TryGetValue(key, out paused) &&
                    entry.Product == entry.Plant.plant.harvestedThingDef &&
                    savedPaused.TryGetValue(entry.Plant.defName, out paused))
                {
                    savedPaused[key] = paused;
                    savedPaused.Remove(entry.Plant.defName);
                }
                var runtime = new RuntimeRule { Entry = entry, Key = key, ProductIndex = index, Paused = paused };
                active.Add(runtime);
                List<RuntimeRule> plantRules;
                if (!byPlant.TryGetValue(entry.Plant, out plantRules))
                    byPlant.Add(entry.Plant, plantRules = new List<RuntimeRule>());
                plantRules.Add(runtime);
            }
            stock = new int[products.Count];
            available = new bool[products.Count];
            builtRevision = FarmingMod.Settings.Revision;
            initialized = true;
        }

        /// <summary>直接读取原版已统计的字典，与 GetCount 使用同一缓存；缺失键不冒充零库存。</summary>
        private void CheckStocks()
        {
            if (active.Count == 0 || map.resourceCounter == null) return;
            Dictionary<ThingDef, int> counts = map.resourceCounter.AllCountedAmounts;
            for (int i = 0; i < products.Count; i++)
                available[i] = counts.TryGetValue(products[i], out stock[i]);
            for (int i = 0; i < active.Count; i++)
            {
                RuntimeRule runtime = active[i];
                runtime.CountAvailable = available[runtime.ProductIndex];
                if (!runtime.CountAvailable) continue; // 未统计：暂不控制，保留原有滞回记忆。
                CropRule rule = runtime.Entry.Rule;
                runtime.Paused = Hysteresis.Next(runtime.Paused, stock[runtime.ProductIndex], rule.Lower, rule.Upper);
                savedPaused[runtime.Key] = runtime.Paused;
            }
        }

        /// <summary>查询植物的库存暂停状态；忽略、无效阈值和关闭总开关均立即解除限制。</summary>
        public bool IsPaused(ThingDef plant)
        {
            List<RuntimeRule> rules;
            if (!FarmingMod.Settings.Enabled || plant == null || !byPlant.TryGetValue(plant, out rules)) return false;
            // 任一未忽略产物仍处于滞回暂停状态，则整种作物保持暂停。
            // 查询只遍历当前作物的小型产物列表，不遍历所有规则或库存。
            foreach (RuntimeRule rule in rules)
                if (!rule.Entry.Rule.Ignore && Hysteresis.Valid(rule.Entry.Rule.Lower, rule.Entry.Rule.Upper)
                    && rule.CountAvailable && rule.Paused) return true;
            return false;
        }
    }
}

