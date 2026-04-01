using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Main looting toil for the complex raid.
/// Every 60 ticks:
///   1. Sends low-HP (< 30%) raiders to retreat individually.
///   2. Checks if sleeping mechs/insects have woken up → group retreat.
///   3. Checks if all accessible crates are in done rooms and no raider
///      has an active assignment → group retreat.
/// Room unclaiming for dead/downed pawns is handled by LordJob.Notify_PawnLost.
/// </summary>
public class LordToil_RHF_ComplexLoot : LordToil
{
    private const int   TickInterval               = 60;
    private const float IndividualRetreatHpFraction = 0.30f;

    public override void UpdateAllDuties()
    {
        foreach (Pawn p in lord.ownedPawns)
        {
            if (p.mindState.duty?.def == DutyDefOf.ExitMapBest) continue;
            p.mindState.duty = new PawnDuty(ReptileHunterFactionDefOf.RHF_ComplexLooterDuty);
        }
    }

    public override void LordToilTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0) return;

        var lordJob = (LordJob_RHF_ComplexLooting)lord.LordJob;
        Map map = lord.Map;

        // --- 1. Individual retreat for low-HP pawns ---
        foreach (Pawn p in lord.ownedPawns)
        {
            if (p.Dead || p.Downed) continue;
            if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (p.mindState.duty?.def == DutyDefOf.ExitMapBest) continue;

            if (p.health.summaryHealth.SummaryHealthPercent < IndividualRetreatHpFraction)
            {
                p.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest);
                p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        // --- 2. Threat awakening check ---
        if (IsThreatAwakened(map))
        {
            lord.ReceiveMemo("ThreatAwakened");
            return;
        }

        // --- 3. Exploration-complete check ---
        // Fire when no crates remain in unexplored rooms AND no raider is mid-room.
        bool anyCratesAccessible = false;
        foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
        {
            if (t is not Building_Crate c || !c.HasAnyContents) continue;
            Room? room = c.GetRoom();
            if (room != null && !lordJob.IsRoomDone(room.ID))
            {
                anyCratesAccessible = true;
                break;
            }
        }

        if (!anyCratesAccessible && !lord.ownedPawns.Any(p => lordJob.HasRoomAssignment(p)))
            lord.ReceiveMemo("AllCratesDone");
    }

    private static bool IsThreatAwakened(Map map)
    {
        foreach (Lord l in map.lordManager.lords)
        {
            if (l.LordJob is LordJob_SleepThenAssaultColony) continue;
            foreach (Pawn p in l.ownedPawns)
            {
                if (p.Spawned && !p.Dead &&
                    (p.RaceProps.IsMechanoid || IsInsectLike(p)))
                    return true;
            }
        }
        return false;
    }

    private static bool IsInsectLike(Pawn p) =>
        p.def.race?.FleshType == FleshTypeDefOf.Insectoid;
}
