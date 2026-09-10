using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace StopFarmingWhenReachLimit
{
    /// <summary>右键菜单专用、线程局部的只读预览上下文，不改变任何配置或库存暂停状态。</summary>
    internal static class FarmingMenuPreview
    {
        internal enum PauseKind { None, Sowing, Harvest }
        [ThreadStatic] internal static PauseKind Current;
        internal static bool Active => Current != PauseKind.None;

        /// <summary>只对本来受库存锁影响的农业目标开启预览；其他原版工作判断保持不变。</summary>
        internal static PauseKind Classify(Pawn pawn, WorkGiverDef giver, LocalTargetInfo target)
        {
            if (!FarmingGate.Enabled(true) && !FarmingGate.Enabled(false)) return PauseKind.None;
            if (pawn == null || pawn.Map == null || giver == null || !target.IsValid || !target.Cell.InBounds(pawn.Map))
                return PauseKind.None;
            WorkGiver worker = giver.Worker;
            if (worker is WorkGiver_GrowerSow)
                return FarmingGate.BlockSowing(pawn.Map, target.Cell) ? PauseKind.Sowing : PauseKind.None;
            if (!(worker is WorkGiver_GrowerHarvest) && !(worker is WorkGiver_PlantsCut)) return PauseKind.None;
            Plant plant = target.HasThing ? target.Thing as Plant : target.Cell.GetPlant(pawn.Map);
            return FarmingGate.BlockHarvest(plant) ? PauseKind.Harvest : PauseKind.None;
        }

        /// <summary>PlantsCut 同时处理砍除与指定收获；实际生成的是砍除任务时不禁用它。</summary>
        internal static void ObserveDesignatedJob(Job job)
        {
            if (Current == PauseKind.Harvest && job != null && !FarmingGate.IsHarvestJob(job)) Current = PauseKind.None;
        }

        /// <summary>在原版等价任务分组之前禁用选项，防止分组缓存保留可点击的收获副本。</summary>
        internal static void DisableOption(FloatMenuOption option)
        {
            if (option == null || !Active) return;
            option.Label += " (" + (Current == PauseKind.Sowing ? "SFRL_MenuPausedSowing" : "SFRL_MenuPausedHarvest").Translate() + ")";
            option.Disabled = true; // 原版 Disabled 属性清空 action，没有可执行的强制工作回调。
            option.autoTakeable = false;
            // DecoratePrioritizedTask 可能添加“强制中断他人”的额外按钮，也必须一并移除。
            option.extraPartOnGUI = null;
            option.extraPartWidth = 0f;
        }
    }

    /// <summary>作用域覆盖 PotentialWorkCellsGlobal 到生成选项全过程，解决区域预过滤导致菜单消失的问题。</summary>
    [HarmonyPatch(typeof(FloatMenuOptionProvider_WorkGivers), "GetWorkGiverOption")]
    internal static class FarmingMenuScopePatch
    {
        /// <summary>保存嵌套上下文，再在无预览状态下判定库存锁；只影响本次同步菜单构建。</summary>
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Pawn pawn, WorkGiverDef workGiver, LocalTargetInfo target,
            out FarmingMenuPreview.PauseKind __state)
        {
            __state = FarmingMenuPreview.Current;
            FarmingMenuPreview.Current = FarmingMenuPreview.PauseKind.None;
            FarmingMenuPreview.Current = FarmingMenuPreview.Classify(pawn, workGiver, target);
        }

        /// <summary>即使原版或其他模组抛出异常也恢复上下文，不吞掉异常，不留下库存锁旁路。</summary>
        private static void Finalizer(FarmingMenuPreview.PauseKind __state)
        {
            FarmingMenuPreview.Current = __state;
        }
    }

    /// <summary>保留原版菜单文字、技能与可达性判断，在原版装饰完成后加入暂停原因。</summary>
    [HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.DecoratePrioritizedTask))]
    internal static class FarmingMenuOptionPatch
    {
        /// <summary>把菜单预览生成的农业选项变为可见但不可选；非农业菜单不受影响。</summary>
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(FloatMenuOption __result)
        {
            FarmingMenuPreview.DisableOption(__result);
        }
    }
}
