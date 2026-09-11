using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>按设施缓存实际植物组成，提前排除整片暂停的收获区域；完全独立于 Smart Farming。</summary>
    public sealed partial class MapComponent_FarmingLimits
    {
        private readonly Dictionary<IPlantToGrowSettable, HarvestArea> harvestAreas =
            new Dictionary<IPlantToGrowSettable, HarvestArea>();
        private readonly Dictionary<IntVec3, HarvestCell> harvestCells = new Dictionary<IntVec3, HarvestCell>();

        /// <summary>设施只保存植物种类计数和订阅格子；不缓存库存判断，避免忽略/删除规则后留下旧锁。</summary>
        private sealed class HarvestArea
        {
            internal IPlantToGrowSettable Grower;
            internal readonly List<HarvestCell> Cells = new List<HarvestCell>();
            internal readonly Dictionary<ThingDef, int> Plants = new Dictionary<ThingDef, int>();
            internal int UncertainCells;
        }

        /// <summary>同格多个设施共享快照，植物变更只重读该格；跨设施重叠时保守回退。</summary>
        private sealed class HarvestCell
        {
            internal IntVec3 Position;
            internal readonly List<HarvestArea> Areas = new List<HarvestArea>(1);
            internal readonly List<ThingDef> Plants = new List<ThingDef>(1);
            internal readonly List<Building_PlantGrower> Growers = new List<Building_PlantGrower>(1);
        }

        /// <summary>只有组成明确、非空且单一实际植物被暂停时才跳过；混种、重叠或右键预览均保留逐株检查。</summary>
        public bool SkipHarvestArea(IPlantToGrowSettable grower)
        {
            if (!FarmingGate.Enabled(false) || FarmingMenuPreview.Active || !FarmingGate.Managed(grower)) return false;
            Zone_Growing zone = grower as Zone_Growing;
            Building_PlantGrower building = grower as Building_PlantGrower;
            if (zone != null ? zone.Map != map : building == null || !building.Spawned || building.Map != map) return false;
            HarvestArea area;
            if (!harvestAreas.TryGetValue(grower, out area))
            {
                area = new HarvestArea { Grower = grower };
                harvestAreas.Add(grower, area);
                if (zone != null)
                {
                    List<IntVec3> cells = zone.Cells;
                    for (int i = 0; i < cells.Count; i++) SubscribeHarvestCell(area, cells[i]);
                }
                else
                {
                    foreach (IntVec3 cell in building.OccupiedRect()) SubscribeHarvestCell(area, cell);
                }
            }
            if (area.UncertainCells != 0 || area.Plants.Count != 1) return false;
            // 不使用当前选择的下一轮作物；按地上实际植物查询已有库存锁。
            foreach (KeyValuePair<ThingDef, int> pair in area.Plants) return IsPaused(pair.Key);
            return false;
        }

        /// <summary>首次建立设施缓存时订阅格子；已有快照可共享，不重复读取重叠格子。</summary>
        private void SubscribeHarvestCell(HarvestArea area, IntVec3 position)
        {
            if (!position.InBounds(map))
            {
                area.UncertainCells++;
                return;
            }
            HarvestCell cell;
            if (!harvestCells.TryGetValue(position, out cell))
            {
                cell = new HarvestCell { Position = position };
                ReadHarvestCell(cell);
                harvestCells.Add(position, cell);
            }
            cell.Areas.Add(area);
            area.Cells.Add(cell);
            ApplyHarvestCell(area, cell, 1);
        }

        /// <summary>仅枚举一个已订阅格子的物体，记录所有实际植物及可能导致归属不明的种植建筑。</summary>
        private void ReadHarvestCell(HarvestCell cell)
        {
            cell.Plants.Clear();
            cell.Growers.Clear();
            List<Thing> things = map.thingGrid.ThingsListAtFast(cell.Position);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Plant plant) cell.Plants.Add(plant.def);
                else if (things[i] is Building_PlantGrower grower) cell.Growers.Add(grower);
            }
        }

        /// <summary>将单格快照加到或移出设施计数；数量归零即移除种类，不随收获积累陈旧种类。</summary>
        private static void ApplyHarvestCell(HarvestArea area, HarvestCell cell, int delta)
        {
            for (int i = 0; i < cell.Plants.Count; i++)
            {
                ThingDef plant = cell.Plants[i];
                int count;
                area.Plants.TryGetValue(plant, out count);
                count += delta;
                if (count == 0) area.Plants.Remove(plant);
                else area.Plants[plant] = count;
            }
            for (int i = 0; i < cell.Growers.Count; i++)
            {
                if (!ReferenceEquals(cell.Growers[i], area.Grower))
                {
                    area.UncertainCells += delta;
                    break;
                }
            }
        }

        /// <summary>植物生成、收获、死亡、移动或建筑变更后更新单格；未缓存的格子无需处理。</summary>
        internal void HarvestCellChanged(IntVec3 position)
        {
            HarvestCell cell;
            if (!harvestCells.TryGetValue(position, out cell)) return;
            for (int i = 0; i < cell.Areas.Count; i++) ApplyHarvestCell(cell.Areas[i], cell, -1);
            ReadHarvestCell(cell);
            for (int i = 0; i < cell.Areas.Count; i++) ApplyHarvestCell(cell.Areas[i], cell, 1);
        }

        /// <summary>区域形状改变或设施移除时注销订阅；下一次派工才重建，不在拖拽的每一格反复扫描。</summary>
        internal void ForgetHarvestArea(IPlantToGrowSettable grower)
        {
            HarvestArea area;
            if (!harvestAreas.TryGetValue(grower, out area)) return;
            harvestAreas.Remove(grower);
            for (int i = 0; i < area.Cells.Count; i++)
            {
                HarvestCell cell = area.Cells[i];
                cell.Areas.Remove(area);
                if (cell.Areas.Count == 0) harvestCells.Remove(cell.Position);
            }
        }
    }

    /// <summary>原版收获器继承基类 ExtraRequirements；在 yield 地格之前排除已暂停的设施。</summary>
    [HarmonyPatch(typeof(WorkGiver_Grower), "ExtraRequirements")]
    internal static class HarvestAreaRequirementsPatch
    {
        /// <summary>仅收紧普通收获器的结果，不干预播种器、其他工作类别或其他模组已有的拒绝。</summary>
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(WorkGiver_Grower __instance, IPlantToGrowSettable settable, Pawn pawn, ref bool __result)
        {
            if (!__result || !(__instance is WorkGiver_GrowerHarvest) || !FarmingGate.Enabled(false)
                || FarmingMenuPreview.Active) return;
            MapComponent_FarmingLimits component = MapComponent_FarmingLimits.For(pawn.Map);
            if (component != null && component.SkipHarvestArea(settable)) __result = false;
        }
    }

    /// <summary>监听原版实际格子登记结果；覆盖播种、收获、销毁、移植、移动及 HDH 输出植物。</summary>
    [HarmonyPatch]
    internal static class HarvestCellRegistrationPatch
    {
        /// <summary>每格回调同时支持多格植物，且不会在无效的外层 Deregister 调用时误减计数。</summary>
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ThingGrid), "RegisterInCell");
            yield return AccessTools.Method(typeof(ThingGrid), "DeregisterInCell");
        }

        /// <summary>绝大多数物体仅经过类型判断；只更新受影响的一格，不遍历地图、设施或库存。</summary>
        private static void Postfix(Thing t, IntVec3 c, Map ___map)
        {
            if (!(t is Plant) && !(t is Building_PlantGrower)) return;
            MapComponent_FarmingLimits component = MapComponent_FarmingLimits.For(___map);
            if (component == null) return;
            if (t is Building_PlantGrower grower) component.ForgetHarvestArea(grower);
            component.HarvestCellChanged(c);
        }
    }

    /// <summary>独立于图标的区域生命周期监听；隐藏图标不会影响收获缓存的正确性。</summary>
    [HarmonyPatch]
    internal static class HarvestZoneShapePatch
    {
        /// <summary>覆盖增减格子及直接注销区域，避免删除的区域留在缓存里。</summary>
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Zone), nameof(Zone.AddCell));
            yield return AccessTools.Method(typeof(Zone), nameof(Zone.RemoveCell));
            yield return AccessTools.Method(typeof(Zone), nameof(Zone.Deregister));
        }

        /// <summary>丢弃变更设施的缓存，其他设施的快照保持可复用。</summary>
        private static void Postfix(Zone __instance)
        {
            if (__instance is Zone_Growing zone) MapComponent_FarmingLimits.For(zone.Map)?.ForgetHarvestArea(zone);
        }
    }
}
