using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace StopFarmingItsEnough
{
    /// <summary>在原版遍历整片田地之前排除暂停播种的设施，避免无谓逐格查找播种工作。</summary>
    [HarmonyPatch(typeof(WorkGiver_GrowerSow), "ExtraRequirements")]
    internal static class SowRequirementsPatch
    {
        /// <summary>保留原版／Smart Farming 的所有拒绝结果，仅额外拒绝被库存锁定的播种。</summary>
        private static void Postfix(IPlantToGrowSettable settable, Pawn pawn, ref bool __result)
        {
            if (__result && FarmingGate.Enabled(true) && FarmingGate.Managed(settable)
                && FarmingGate.Paused(pawn.Map, settable.GetPlantDefToGrow(), true)) __result = false;
        }
    }

    /// <summary>覆盖直接派工和右键优先播种；不改写 Smart Farming 的季节检查与模式。</summary>
    [HarmonyPatch(typeof(WorkGiver_GrowerSow), "JobOnCell")]
    internal static class SowJobPatch
    {
        /// <summary>尽早拒绝库存暂停目标；优先级较低让 HDH 先建立并正常清理其温度检查上下文。</summary>
        [HarmonyPriority(Priority.Last)]
        [HarmonyAfter("owlchemist.smartfarming", "MapleApple.HighDensityHydroponics.Fixed.ZouHb.zades")]
        private static bool Prefix(Pawn pawn, IntVec3 c, ref Job __result)
        {
            if (!FarmingGate.BlockSowing(pawn.Map, c)) return true;
            __result = null;
            return false;
        }

        /// <summary>最终只收紧结果；其他模组生成的实际播种任务仍必须通过库存锁。</summary>
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Pawn pawn, IntVec3 c, ref Job __result)
        {
            if (__result != null && FarmingGate.BlockSowing(pawn.Map, c)) __result = null;
        }
    }

    /// <summary>原版没有独立库存收获开关，因此在收获资格检查中实现。</summary>
    [HarmonyPatch(typeof(WorkGiver_GrowerHarvest), "HasJobOnCell")]
    internal static class HarvestCellPatch
    {
        /// <summary>逐株兜底：设施预过滤放行的混种、重叠及直接派工仍按实际植物判断。</summary>
        private static bool Prefix(Pawn pawn, IntVec3 c, ref bool __result)
        {
            if (!FarmingGate.Enabled(false) || !c.InBounds(pawn.Map) || !FarmingGate.BlockHarvest(c.GetPlant(pawn.Map))) return true;
            __result = false;
            return false;
        }

        /// <summary>保留 Smart Farming 的“允许收获”拒绝结果，库存规则只会追加限制。</summary>
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Pawn pawn, IntVec3 c, ref bool __result)
        {
            if (__result && FarmingGate.Enabled(false) && c.InBounds(pawn.Map) && FarmingGate.BlockHarvest(c.GetPlant(pawn.Map))) __result = false;
        }
    }

    /// <summary>覆盖手动收获标记和 Smart Farming 的立即收获标记。</summary>
    [HarmonyPatch(typeof(WorkGiver_PlantsCut), "JobOnThing")]
    internal static class DesignatedHarvestPatch
    {
        /// <summary>仅过滤收获任务，保留玩家的砍除指令和植物清理工作。</summary>
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Thing t, ref Job __result)
        {
            FarmingMenuPreview.ObserveDesignatedJob(__result);
            if (FarmingGate.IsHarvestJob(__result) && FarmingGate.BlockHarvest(t as Plant)) __result = null;
        }
    }

    /// <summary>给播种任务添加轻量结束条件，覆盖库存检查之前已被小人领取的任务。</summary>
    [HarmonyPatch(typeof(JobDriver_PlantSow), "MakeNewToils")]
    internal static class SowDriverPatch
    {
        /// <summary>在任务初始化（含读档重建）时安装一次结束条件；每次只查目标和缓存状态。</summary>
        private static void Postfix(JobDriver_PlantSow __instance)
        {
            __instance.AddEndCondition(() => FarmingGate.SowEndCondition(__instance));
        }
    }

    /// <summary>收获任务会排队多个植物，必须在执行阶段继续检查以实现严格暂停。</summary>
    [HarmonyPatch(typeof(JobDriver_PlantWork), "MakeNewToils")]
    internal static class HarvestDriverPatch
    {
        /// <summary>只给 Harvest/HarvestDesignated 添加条件，不给普通砍除任务增加持续检查。</summary>
        private static void Postfix(JobDriver_PlantWork __instance)
        {
            if (FarmingGate.IsHarvestJob(__instance.job))
                __instance.AddEndCondition(() => FarmingGate.HarvestEndCondition(__instance));
        }
    }

}


