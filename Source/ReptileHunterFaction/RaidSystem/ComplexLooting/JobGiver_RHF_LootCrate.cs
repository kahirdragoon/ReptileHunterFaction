using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Think node for the RHF_ComplexLooterDuty duty.
/// Assigns the raider to the nearest unchecked indoor room and issues an
/// RHF_ExploreRoom job targeting a reachable cell inside that room.
/// Only one raider is ever assigned to a given room at a time.
/// </summary>
public class JobGiver_RHF_ExploreRoom : ThinkNode_JobGiver
{
    protected override Job? TryGiveJob(Pawn pawn)
    {
        var lordJob = pawn.GetLord()?.LordJob as LordJob_RHF_ComplexLooting;
        if (lordJob == null) return null;

        Map map = pawn.Map;

        // Enumerate all unique indoor rooms on the map and find the nearest
        // one that hasn't been explored yet and isn't currently assigned.
        var seenRoomIDs = new HashSet<int>();
        Room?   bestRoom = null;
        IntVec3 bestCell = default;
        float   bestDist = float.MaxValue;

        foreach (IntVec3 c in map.AllCells)
        {
            Room? room = c.GetRoom(map);
            if (room == null || room.UsesOutdoorTemperature || room.IsHuge) continue;
            if (room.CellCount <= 1) continue;                   // skip doorway micro-rooms
            if (!seenRoomIDs.Add(room.ID)) continue;             // already considered
            if (lordJob.IsRoomDone(room.ID)) continue;
            if (lordJob.IsRoomAssigned(room.ID)) continue;

            // Find the first reachable standable cell in this room
            IntVec3 targetCell = default;
            foreach (IntVec3 roomCell in room.Cells)
            {
                if (roomCell.Standable(map) && pawn.CanReach(roomCell, PathEndMode.OnCell, Danger.Some))
                {
                    targetCell = roomCell;
                    break;
                }
            }
            if (!targetCell.IsValid) continue;

            float dist = targetCell.DistanceToSquared(pawn.Position);
            if (dist < bestDist) { bestDist = dist; bestRoom = room; bestCell = targetCell; }
        }

        if (bestRoom == null) return null;
        if (!lordJob.TryAssignRoom(pawn, bestRoom.ID)) return null;

        return JobMaker.MakeJob(ReptileHunterFactionDefOf.RHF_ExploreRoom, bestCell);
    }
}
