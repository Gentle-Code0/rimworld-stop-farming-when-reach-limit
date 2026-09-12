using RimWorld;
using Verse;
using Verse.AI;

namespace StopFarmingItsEnough
{
    /// <summary>播种／收获统一入口；只做当前目标的格子查询和状态查询，不生成库存扫描。</summary>
    public static class FarmingGate
    {
        /// <summary>快速检查指定工作类别是否启用自动控制。</summary>
        public static bool Enabled(bool sowing)
        {
            FarmingSettings settings = FarmingMod.Settings;
            return settings != null && settings.Enabled && (sowing ? settings.ControlSowing : settings.ControlHarvest);
        }

        /// <summary>仅管理玩家种植区和玩家种植设施，排除敌方水培箱及无种植设施的野生植物。</summary>
        public static bool Managed(IPlantToGrowSettable grower)
        {
            if (grower is Zone_Growing) return true;
            Building_PlantGrower building = grower as Building_PlantGrower;
            return building != null && building.Faction == Faction.OfPlayer;
        }

        /// <summary>查询地图＋实际植物类型的库存锁；调用者负责检查目标是否属于本模组管理范围。</summary>
        public static bool Paused(Map map, ThingDef plant, bool sowing)
        {
            // 仅在同步生成右键菜单的作用域内模拟“没有库存锁”，让原版生成完整选项；
            // 选项在交给菜单前立即设为 Disabled，作用域在异常路径也会恢复，绝不执行工作。
            if (!Enabled(sowing) || FarmingMenuPreview.Active) return false;
            MapComponent_FarmingLimits component = MapComponent_FarmingLimits.For(map);
            return component != null && component.IsPaused(plant, sowing);
        }

        /// <summary>以当前设施设定的作物判断播种；不修改 allowSow 或 Smart Farming 的 sowMode。</summary>
        public static bool BlockSowing(Map map, IntVec3 cell)
        {
            if (!Enabled(true) || map == null || !cell.InBounds(map)) return false;
            IPlantToGrowSettable grower = cell.GetPlantToGrowSettable(map);
            return Managed(grower) && Paused(map, grower.GetPlantDefToGrow(), true);
        }

        /// <summary>以地上待收植物的实际 def 判断收获，确保改种后的旧作物使用自己的阈值。</summary>
        public static bool BlockHarvest(Plant plant)
        {
            if (!Enabled(false) || plant == null || !plant.Spawned) return false;
            return Paused(plant.Map, plant.def, false) && Managed(plant.Position.GetPlantToGrowSettable(plant.Map));
        }

        /// <summary>识别真正的收获任务，避免把砍除、清理枯萎植物等工作当成收获。</summary>
        public static bool IsHarvestJob(Job job)
        {
            return job != null && (job.def == JobDefOf.Harvest || job.def == JobDefOf.HarvestDesignated);
        }

        /// <summary>严格暂停正在执行的播种任务；原版清理动作会移除未完成的播种幼苗。</summary>
        public static JobCondition SowEndCondition(JobDriver driver)
        {
            if (!Enabled(true) || driver.pawn.Map == null || driver.job == null) return JobCondition.Ongoing;
            IntVec3 cell = driver.job.targetA.Cell;
            if (!cell.InBounds(driver.pawn.Map)) return JobCondition.Ongoing;
            IPlantToGrowSettable grower = cell.GetPlantToGrowSettable(driver.pawn.Map);
            // 使用 job.plantDefToSow：设施在小人移动途中改种，也不能用新作物规则放行旧任务。
            return Managed(grower) && Paused(driver.pawn.Map, driver.job.plantDefToSow, true)
                ? JobCondition.Incompletable : JobCondition.Ongoing;
        }

        /// <summary>阻止已领取的收获队列继续绕过库存锁；目标切换后自动检查下一株。</summary>
        public static JobCondition HarvestEndCondition(JobDriver driver)
        {
            return IsHarvestJob(driver.job) && BlockHarvest(driver.job.targetA.Thing as Plant)
                ? JobCondition.Incompletable : JobCondition.Ongoing;
        }

        /// <summary>根据当前植物状态生成本地化说明，帮助玩家区分库存暂停和手动禁止。</summary>
        public static string InspectStatus(Map map, ThingDef plant)
        {
            MapComponent_FarmingLimits component = MapComponent_FarmingLimits.For(map);
            if (component == null) return null;
            bool sow = Enabled(true) && component.IsPaused(plant, true),
                harvest = Enabled(false) && component.IsPaused(plant, false);
            if (!sow && !harvest) return null;
            return (sow && harvest ? "SFIE_PausedBoth" : sow ? "SFIE_PausedSowing" : "SFIE_PausedHarvest").Translate(plant.LabelCap);
        }
    }
}


