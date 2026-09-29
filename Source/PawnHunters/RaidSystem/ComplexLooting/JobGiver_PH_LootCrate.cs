using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnHunters;

/// <summary>
/// Think node for the PH_ComplexLooterDuty duty.
/// Assigns the raider to the nearest unchecked indoor room and issues an
/// PH_ExploreRoom job targeting a reachable cell inside that room.
/// Only one raider is ever assigned to a given room at a time.
/// </summary>
public class JobGiver_PH_ExploreRoom : ThinkNode_JobGiver
{
    public override Job? TryGiveJob(Pawn pawn)
    {
        var lordJob = pawn.GetLord()?.LordJob as LordJob_PH_ComplexLooting;
        if (lordJob == null) return null;

        Map map = pawn.Map;

        // Enumerate all indoor rooms on the map and find the nearest one that hasn't been explored yet
        // and isn't currently assigned. Rooms are rebuilt first so the list can't change while we read it.
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
        IReadOnlyList<Room> rooms = map.regionGrid.AllRooms;

        Room?   bestRoom = null;
        IntVec3 bestCell = IntVec3.Invalid;
        int     bestDist = int.MaxValue;

        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room.UsesOutdoorTemperature || room.IsHuge) continue;
            if (room.CellCount <= 1) continue;                   // skip doorway micro-rooms
            if (lordJob.IsRoomDone(room)) continue;
            if (lordJob.IsRoomAssigned(room)) continue;

            IntVec3 targetCell = FirstReachableStandableCell(pawn, room, map);
            if (!targetCell.IsValid) continue;

            int dist = targetCell.DistanceToSquared(pawn.Position);
            if (dist < bestDist) { bestDist = dist; bestRoom = room; bestCell = targetCell; }
        }

        if (bestRoom == null) return null;
        if (!lordJob.TryAssignRoom(pawn, bestRoom)) return null;

        return JobMaker.MakeJob(PawnHuntersDefOf.PH_ExploreRoom, bestCell);
    }

    /// <summary>
    /// The room's first standable cell (in Room.Cells order) the pawn can reach, or Invalid. Reachability is the
    /// same for every cell of a region, so only the first standable cell of each region is checked: a locked
    /// room costs one check per region instead of one per cell.
    /// </summary>
    private static IntVec3 FirstReachableStandableCell(Pawn pawn, Room room, Map map)
    {
        foreach (District district in room.Districts)
        {
            foreach (Region region in district.Regions)
            {
                foreach (IntVec3 cell in region.Cells)
                {
                    if (!cell.Standable(map)) continue;
                    if (pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some)) return cell;
                    break; // the rest of this region is just as unreachable
                }
            }
        }
        return IntVec3.Invalid;
    }
}
