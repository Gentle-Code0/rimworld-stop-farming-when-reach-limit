using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>在游戏实际枚举额外收获物时旁观产物；绝不创建样本植物或提前执行收获代码。</summary>
    [HarmonyPatch]
    internal static class AdditionalHarvestDiscovery
    {
        /// <summary>仅补丁已知作物组件的标准额外收获接口；继承相同实现的组件只补丁一次。</summary>
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new HashSet<MethodBase>();
            methods.Add(AccessTools.Method(typeof(ThingComp), nameof(ThingComp.GetAdditionalHarvestYield)));
            foreach (ThingDef plant in CropCatalog.Plants)
            {
                if (plant.comps == null) continue;
                foreach (CompProperties comp in plant.comps)
                {
                    if (comp.compClass == null) continue;
                    MethodInfo method = AccessTools.Method(comp.compClass, nameof(ThingComp.GetAdditionalHarvestYield));
                    if (method != null && !method.IsAbstract &&
                        method.ReturnType == typeof(IEnumerable<ThingDefCountClass>)) methods.Add(method);
                }
            }
            return methods;
        }

        /// <summary>为真实植物的收获枚举附加观察器，原始结果和执行时机保持不变。</summary>
        private static void Postfix(ThingComp __instance, ref IEnumerable<ThingDefCountClass> __result)
        {
            if (__result != null && __instance.parent is Plant)
                __result = Observe(__result, __instance.parent.def);
        }

        /// <summary>逐项原样传递产物，仅登记数量大于零的产物；库存仍由地图资源统计提供。</summary>
        private static IEnumerable<ThingDefCountClass> Observe(IEnumerable<ThingDefCountClass> source, ThingDef plant)
        {
            foreach (ThingDefCountClass item in source)
            {
                if (item != null && item.thingDef != null && item.count > 0)
                    CropCatalog.RegisterProduct(plant, item.thingDef);
                yield return item;
            }
        }
    }
}

