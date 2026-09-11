using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>仅按稳定定义分类未来普通自动收获的可能性；不读取 Growth、LifeStage 或 HarvestableNow。</summary>
    internal static class HarvestEligibility
    {
        private static readonly Dictionary<ThingDef, bool> candidates = new Dictionary<ThingDef, bool>();
        private static bool checkedHooks;
        private static bool uncertainHooks;

        /// <summary>每种定义首次出现时判断一次。标准模组定义与原版同等处理，自定义代码一律保守纳入。</summary>
        internal static bool MayAutoHarvest(ThingDef def)
        {
            if (def == null) return true;
            bool candidate;
            if (candidates.TryGetValue(def, out candidate)) return candidate;
            if (!checkedHooks)
            {
                // 首次实际搜索时各模组启动补丁应已安装；检测仅做一次，不进入逐 tick 热路径。
                uncertainHooks = HasForeignPatch(AccessTools.Method(typeof(WorkGiver_GrowerHarvest), "HasJobOnCell"))
                    || HasForeignPatch(AccessTools.PropertyGetter(typeof(Plant), "HarvestableNow"))
                    || HasForeignPatch(AccessTools.PropertyGetter(typeof(PlantProperties), "Harvestable"));
                checkedHooks = true;
            }
            // Harvestable 由原版 harvestYield 派生，不能仅用 harvestedThingDef 非空与否推断，
            // 因为其他模组可能通过额外收获接口提供产物。
            candidate = uncertainHooks || def.plant == null || def.thingClass != typeof(Plant)
                || (def.comps != null && def.comps.Count != 0)
                || (def.plant.Harvestable && def.plant.autoHarvestable);
            candidates.Add(def, candidate);
            return candidate;
        }

        /// <summary>陌生补丁可能改变原版资格逻辑，退回原来的全植物缓存；不猜测它是否只会拒绝工作。</summary>
        private static bool HasForeignPatch(MethodBase method)
        {
            if (method == null) return true;
            Patches patches = Harmony.GetPatchInfo(method);
            if (patches == null) return false;
            return ContainsForeign(patches.Prefixes) || ContainsForeign(patches.Postfixes)
                || ContainsForeign(patches.Transpilers) || ContainsForeign(patches.Finalizers);
        }

        /// <summary>本程序集的库存补丁只收紧资格；其他程序集的补丁被视为未知收获规则。</summary>
        private static bool ContainsForeign(IEnumerable<Patch> patches)
        {
            foreach (Patch patch in patches)
                if (patch.PatchMethod == null || patch.PatchMethod.Module.Assembly != typeof(FarmingMod).Assembly) return true;
            return false;
        }
    }
}
