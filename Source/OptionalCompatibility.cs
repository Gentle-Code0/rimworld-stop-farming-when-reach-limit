using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>可选兼容层；使用运行时类型检测，程序集不静态引用任一农业模组。</summary>
    internal static class OptionalCompatibility
    {
        public static bool SmartFarmingActive { get; private set; }
        public static bool HighDensityReady { get; private set; }

        /// <summary>检测已加载程序集并安装 HDH 独立入口；Smart Farming 沿用它自己的模式和补丁。</summary>
        public static void Install(Harmony harmony)
        {
            SmartFarmingActive = AccessTools.TypeByName("SmartFarming.Mod_SmartFarming") != null;
            Type hydro = AccessTools.TypeByName("HighDensityHydro.Building_HighDensityHydro");
            if (hydro == null) return;
            MethodInfo sow = AccessTools.DeclaredMethod(hydro, "CanAcceptSowNowInternal");
            MethodInfo harvest = AccessTools.DeclaredMethod(hydro, "HandleHarvest");
            FieldInfo currentPlant = AccessTools.Field(hydro, "_currentPlantDefToGrow");
            if (sow == null || sow.ReturnType != typeof(bool) || sow.GetParameters().Length != 0
                || harvest == null || harvest.ReturnType != typeof(void) || harvest.GetParameters().Length != 0
                || currentPlant == null || currentPlant.FieldType != typeof(ThingDef))
            {
                Log.Error("[Stop farming when reach limit] HDH API differs from the supported 1.6 version; dedicated compatibility was not installed.");
                return;
            }
            harmony.Patch(sow, postfix: new HarmonyMethod(typeof(OptionalCompatibility), nameof(HydroSowPostfix)) { priority = Priority.Last });
            harmony.Patch(harvest, prefix: new HarmonyMethod(typeof(OptionalCompatibility), nameof(HydroHarvestPrefix)));
            HighDensityReady = true;
        }

        /// <summary>严格拒绝新播种，半满箱也不放行；不跳过 HandleSowing，以便吸收已完成的幼苗并自然转换阶段。</summary>
        private static void HydroSowPostfix(Building_PlantGrower __instance, ref bool __result)
        {
            if (__result && FarmingGate.Managed(__instance)
                && FarmingGate.Paused(__instance.Map, __instance.GetPlantDefToGrow(), true)) __result = false;
        }

        /// <summary>收获暂停时停止内部批次向地面输出；使用当前批次植物而非下一批设定，保留外部生命周期更新。</summary>
        private static bool HydroHarvestPrefix(Building_PlantGrower __instance, ThingDef ____currentPlantDefToGrow)
        {
            // Harmony 的 ___前缀 + 字段原名 _currentPlantDefToGrow = 四条下划线。
            // 这里只阻止收获阶段处理：TickRare/TickLong 的衰老、环境伤害及电力逻辑照常运行。
            return !FarmingGate.Managed(__instance)
                || !FarmingGate.Paused(__instance.Map, ____currentPlantDefToGrow, false);
        }
    }
}
