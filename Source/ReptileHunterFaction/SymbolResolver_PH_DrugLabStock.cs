using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ReptileHunterFaction;

/// <summary>
/// Post-processes the PH_DrugLab settlement structure after generation.
///
/// DrugLab room  — clears shelves, fills with Neutroamine (80–90%) and Luciferium (10–20%).
/// Drug storage room — removes the RHF_DrugMedicine marker, clears shelves, fills ~3/4 with
///   RHF_DrugMedicine and ~1/4 with RHF_SBD (all stacks 60–100% full). Sets wall coolers to -9°C.
/// </summary>
public class SymbolResolver_PH_DrugLabStock : SymbolResolver
{
    private const float CoolerTargetTemp = -9f;

    public override void Resolve(ResolveParams rp)
    {
        Map map = BaseGen.globalSettings.map;
        if (map == null) return;

        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

        FillDrugLabRoom(map, rp.rect);
        FillDrugStorageRoom(map, rp.rect);
        FillDrugSafeRoom(map, rp.rect);
    }

    // ── DrugLab room ─────────────────────────────────────────────────────────

    private static void FillDrugLabRoom(Map map, CellRect settlementRect)
    {
        // Multiple PH_DrugLab structures may be placed; find all distinct rooms that contain a DrugLab.
        var rooms = map.listerThings.GetThingsOfType<Building>()
            .Where(b => b.def == VanillaDefOf.DrugLab && settlementRect.Contains(b.Position))
            .Select(b => b.GetRoom())
            .Where(r => r != null)
            .Distinct()
            .ToList();

        foreach (var room in rooms)
        {
            var shelves = GetShelvesInRoom(map, room!, settlementRect);
            ClearShelfItems(shelves, map);

            var cells = GetShelfCells(shelves);
            if (cells.Count == 0) continue;

            cells = cells.InRandomOrder().ToList();
            int neutroamineSlots = Mathf.RoundToInt(cells.Count * Rand.Range(0.75f, 0.90f));
            SpawnItemsOnCells(cells.Take(neutroamineSlots).ToList(), VanillaDefOf.Neutroamine, map);
            SpawnItemsOnCells(cells.Skip(neutroamineSlots).ToList(), ThingDefOf.Luciferium, map, 3, 10);
        }
    }

    // ── Drug storage room ─────────────────────────────────────────────────────

    private static void FillDrugStorageRoom(Map map, CellRect settlementRect)
    {
        // Each PH_DrugLab structure places one RHF_DrugMedicine item as a room marker.
        // Collect one representative marker per distinct room and process each independently.
        var markersByRoom = map.listerThings.ThingsOfDef(ReptileHunterFactionDefOf.RHF_DrugMedicine)
            .Where(t => settlementRect.Contains(t.Position) && t is not Building)
            .GroupBy(t => t.GetRoom())
            .Where(g => g.Key != null)
            .Select(g => (marker: g.First(), room: g.Key!))
            .ToList();

        foreach (var (marker, room) in markersByRoom)
        {
            marker.Destroy();

            var shelves = GetShelvesInRoom(map, room, settlementRect);
            ClearShelfItems(shelves, map);

            var cells = GetShelfCells(shelves);
            if (cells.Count == 0) continue;

            SpawnItemsOnCells(cells.ToList(), ReptileHunterFactionDefOf.RHF_DrugMedicine, map);

            SetRoomCoolerTemperature(map, room, settlementRect, CoolerTargetTemp);
        }
    }

    // ── Drug safe ─────────────────────────────────────────────────────
    private static void FillDrugSafeRoom(Map map, CellRect settlementRect)
    {
        var markersByRoom = map.listerThings.ThingsOfDef(ReptileHunterFactionDefOf.RHF_SBD)
            .Where(t => settlementRect.Contains(t.Position) && t is not Building)
            .GroupBy(t => t.GetRoom())
            .Where(g => g.Key != null)
            .Select(g => (marker: g.First(), room: g.Key!))
            .ToList();

        foreach (var (marker, room) in markersByRoom)
        {
            marker.Destroy();

            var shelves = GetShelvesInRoom(map, room, settlementRect);
            ClearShelfItems(shelves, map);

            var cells = GetShelfCells(shelves);
            if (cells.Count == 0) continue;

            SpawnItemsOnCells(cells.ToList(), ReptileHunterFactionDefOf.RHF_SBD, map, 1, 6);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<Building_Storage> GetShelvesInRoom(Map map, Room room, CellRect settlementRect)
    {
        return map.listerThings.GetThingsOfType<Building_Storage>()
            .Where(s => settlementRect.Contains(s.Position) && s.GetRoom() == room)
            .ToList();
    }

    private static void ClearShelfItems(List<Building_Storage> shelves, Map map)
    {
        foreach (var shelf in shelves)
        {
            foreach (var cell in shelf.OccupiedRect())
            {
                var things = cell.GetThingList(map).Where(t => t is not Building).ToList();
                foreach (var thing in things)
                    thing.Destroy();
            }
        }
    }

    private static List<IntVec3> GetShelfCells(List<Building_Storage> shelves)
    {
        var cells = new List<IntVec3>();
        foreach (var shelf in shelves)
            cells.AddRange(shelf.OccupiedRect().Cells);
        return cells;
    }

    /// <summary>Spawns 1–3 stacks of <paramref name="def"/> on each cell (shelves hold up to 3 stacks).</summary>
    private static void SpawnItemsOnCells(List<IntVec3> cells, ThingDef def, Map map, int minStacksize = 3, int maxStacksize = 20)
    {
        foreach (var cell in cells)
        {
            int stacksToSpawn = Rand.Range(1, 4); // 1–3 inclusive
            for (int i = 0; i < stacksToSpawn; i++)
            {
                var thing = ThingMaker.MakeThing(def);
                thing.stackCount = Rand.Range(minStacksize, maxStacksize + 1); // 3–20 inclusive
                GenSpawn.Spawn(thing, cell, map);
            }
        }
    }

    /// <summary>Finds coolers whose cold side faces into <paramref name="room"/> and sets their target temperature.</summary>
    private static void SetRoomCoolerTemperature(Map map, Room room, CellRect settlementRect, float temperature)
    {
        foreach (var cooler in map.listerThings.GetThingsOfType<Building>()
            .Where(b => b.def == ThingDefOf.Cooler && settlementRect.Contains(b.Position)))
        {
            var coldSideCell = cooler.Position + IntVec3.South.RotatedBy(cooler.Rotation);
            if (coldSideCell.GetRoom(map) != room) continue;

            var tempControl = cooler.GetComp<CompTempControl>();
            if (tempControl != null)
                tempControl.targetTemperature = temperature;
        }
    }
}
