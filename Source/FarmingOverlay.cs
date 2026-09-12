using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>地图标记缓存，与库存控制共用地图组件；不扫描库存、不扫描所有植物。</summary>
    public sealed partial class MapComponent_FarmingLimits
    {
        private readonly List<OverlayMarker> markers = new List<OverlayMarker>();
        private Dictionary<Zone_Growing, OverlayMarker> zoneMarkers = new Dictionary<Zone_Growing, OverlayMarker>();
        private Dictionary<Zone_Growing, OverlayMarker> spareZoneMarkers = new Dictionary<Zone_Growing, OverlayMarker>();
        private bool overlayDirty = true;
        private int overlayTick = -CheckInterval;
        private int zoneCount = -1, buildingCount = -1;

        /// <summary>缓存设施引用及种植区锚点；形状未改变时不再次遍历种植区格子。</summary>
        private sealed class OverlayMarker
        {
            internal Zone_Growing Zone;
            internal Building_PlantGrower Building;
            internal IntVec3 Anchor;
            internal int CellCount = -1;
        }

        /// <summary>种植区形状编辑后使对应锚点失效，包括格数相同但形状变化的情况。</summary>
        internal void InvalidateOverlay(Zone_Growing zone)
        {
            OverlayMarker marker;
            if (zoneMarkers.TryGetValue(zone, out marker)) marker.CellCount = -1;
            overlayDirty = true;
        }

        /// <summary>仅在当前可见地图绘制；屏外和迷雾中的设施不绘制，不要求玩家先选中。</summary>
        public override void MapComponentOnGUI()
        {
            if (map != Find.CurrentMap || !WorldRendererUtility.DrawingMap
                || (!FarmingGate.Enabled(true) && !FarmingGate.Enabled(false))) return;
            int tick = Find.TickManager.TicksGame;
            // O(1) 检查列表数量；600 ticks 兜底发现同数量的建筑替换等情况。
            if (overlayDirty || zoneCount != map.zoneManager.AllZones.Count
                || buildingCount != map.listerBuildings.allBuildingsColonist.Count || tick - overlayTick >= CheckInterval)
                RefreshMarkers(tick);
            CellRect view = Find.CameraDriver.CurrentViewRect;
            for (int i = 0; i < markers.Count; i++)
            {
                OverlayMarker marker = markers[i];
                Building_PlantGrower building = marker.Building;
                if (building != null && (!building.Spawned || building.Map != map || !FarmingGate.Managed(building))) continue;
                IntVec3 anchor = building != null ? building.Position : marker.Anchor;
                if (!anchor.InBounds(map) || !view.Contains(anchor) || anchor.Fogged(map)) continue;
                ThingDef sowPlant = building != null ? building.GetPlantDefToGrow() : marker.Zone.GetPlantDefToGrow();
                ThingDef harvestPlant = building != null ? OptionalCompatibility.HarvestPlantFor(building) : sowPlant;
                bool pausedSow = !FarmingMod.Settings.HideSowingIcon && FarmingGate.Enabled(true) && IsPaused(sowPlant, true);
                bool pausedHarvest = !FarmingMod.Settings.HideHarvestIcon && FarmingGate.Enabled(false) && IsPaused(harvestPlant, false);
                if (!pausedSow && !pausedHarvest) continue;
                Vector2 position = building != null ? GenMapUI.LabelDrawPosFor(building, 0f) : GenMapUI.LabelDrawPosFor(anchor);
                int count = pausedSow && pausedHarvest ? 2 : 1;
                if (pausedSow) PauseIcons.Draw(PauseIcons.Slot(position, 0, count), true);
                if (pausedHarvest) PauseIcons.Draw(PauseIcons.Slot(position, pausedSow ? 1 : 0, count), false);
            }
        }

        /// <summary>只枚举原版种植区列表和玩家建筑列表建立引用缓存；不会遍历库存或全地图地格。</summary>
        private void RefreshMarkers(int tick)
        {
            markers.Clear();
            spareZoneMarkers.Clear();
            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = 0; i < zones.Count; i++)
            {
                Zone_Growing zone = zones[i] as Zone_Growing;
                if (zone == null || zone.Cells.Count == 0) continue;
                OverlayMarker marker;
                if (!zoneMarkers.TryGetValue(zone, out marker)) marker = new OverlayMarker { Zone = zone };
                if (marker.CellCount != zone.Cells.Count) UpdateAnchor(marker);
                spareZoneMarkers.Add(zone, marker);
                markers.Add(marker);
            }
            var previous = zoneMarkers;
            zoneMarkers = spareZoneMarkers;
            spareZoneMarkers = previous;
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i] is Building_PlantGrower grower) markers.Add(new OverlayMarker { Building = grower });
            zoneCount = zones.Count;
            buildingCount = buildings.Count;
            overlayTick = tick;
            overlayDirty = false;
        }

        /// <summary>首次显示或编辑形状时寻找最靠近质心的实际格子，避免 L 形区域的标记落在区域外。</summary>
        private static void UpdateAnchor(OverlayMarker marker)
        {
            List<IntVec3> cells = marker.Zone.Cells;
            double x = 0, z = 0;
            for (int i = 0; i < cells.Count; i++) { x += cells[i].x; z += cells[i].z; }
            x /= cells.Count;
            z /= cells.Count;
            double best = double.MaxValue;
            for (int i = 0; i < cells.Count; i++)
            {
                double dx = cells[i].x - x, dz = cells[i].z - z;
                double distance = dx * dx + dz * dz;
                if (distance < best) { best = distance; marker.Anchor = cells[i]; }
            }
            marker.CellCount = cells.Count;
        }
    }

    /// <summary>跟踪种植区形状变化，不在绘制期间反复扫描格子计算中心。</summary>
    [HarmonyPatch]
    internal static class GrowingZoneShapePatch
    {
        /// <summary>两种编辑入口共享同一个失效回调，其他类型的区域不受影响。</summary>
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Zone), nameof(Zone.AddCell));
            yield return AccessTools.Method(typeof(Zone), nameof(Zone.RemoveCell));
        }

        /// <summary>标记需要更新的种植区；一次拖拽的多次修改只会在下一次绘制时统一更新。</summary>
        private static void Postfix(Zone __instance)
        {
            if (__instance is Zone_Growing zone) MapComponent_FarmingLimits.For(zone.Map)?.InvalidateOverlay(zone);
        }
    }
}


