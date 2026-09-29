using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnHunters;

/// <summary>
/// Retreat toil for the big kidnapping raid.
/// Free raiders flee using ExitMapBestAndDefendSelf.
/// Active kidnappers finish carrying their target off map.
/// Active corpse carriers finish carrying their corpse off map.
/// Each tick, free retreating raiders within 2 tiles of a downed player pawn or
/// player corpse opportunistically grab it before leaving.
/// </summary>
public class LordToil_PH_RetreatWithCarry : LordToil
{
    private const int OpportunisticGrabRange = 2;
    private const int TickInterval           = 60;

    // Lord.GotoToil calls UpdateAllDuties right after Init, so no Init override is needed.
    public override void UpdateAllDuties()
    {
        var lordJob = (LordJob_PH_KidnappingRaidBig)lord.LordJob;

        foreach (Pawn p in lord.ownedPawns)
        {
            if (lordJob.IsKidnapper(p) && lordJob.GetTargetFor(p) != null)
            {
                p.mindState.duty = new PawnDuty(PawnHuntersDefOf.PH_KidnaperDuty_Big);
            }
            else if (lordJob.IsCorpseCarrier(p))
            {
                p.mindState.duty = new PawnDuty(PawnHuntersDefOf.PH_CarryCorpseDuty);
            }
            else if (p.mindState.duty?.def != DutyDefOf.ExitMapBestAndDefendSelf)
            {
                // Interrupt only on the switch to retreating, not every time duties are refreshed.
                p.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBestAndDefendSelf);
                p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }

    public override void LordToilTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0) return;

        var lordJob = (LordJob_PH_KidnappingRaidBig)lord.LordJob;
        Map map = lord.Map;

        lordJob.ValidateKidnaps();
        lordJob.ValidateCorpseCarriers();

        TryOpportunisticGrab(lordJob, map);
    }

    // TryAssignKidnapper / TryAssignCorpseCarrier already set the grabbing raider's duty and restart its job;
    // the other retreating raiders are left alone.
    private void TryOpportunisticGrab(LordJob_PH_KidnappingRaidBig lordJob, Map map)
    {
        foreach (Pawn raider in lord.ownedPawns)
        {
            if (raider.Dead || raider.Downed || !raider.Spawned) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (lordJob.IsKidnapper(raider) || lordJob.IsCorpseCarrier(raider)) continue;

            switch (FindGrabTarget(raider, map, lordJob))
            {
                case Pawn downed:   lordJob.TryAssignKidnapper(downed, raider);    break;
                case Corpse corpse: lordJob.TryAssignCorpseCarrier(corpse, raider); break;
            }
        }
    }

    /// <summary>
    /// The closest downed target within OpportunisticGrabRange (priority 1), else the closest player corpse
    /// (priority 2). Only the cells around the raider are checked: the radial pattern is sorted by distance,
    /// so the first match is the closest, and the cost doesn't grow with the colony or its corpse stockpile.
    /// </summary>
    private static Thing? FindGrabTarget(Pawn raider, Map map, LordJob_PH_KidnappingRaidBig lordJob)
    {
        Corpse? closestCorpse = null;
        int     numCells      = GenRadial.NumCellsInRadius(OpportunisticGrabRange);

        for (int i = 0; i < numCells; i++)
        {
            IntVec3 cell = raider.Position + GenRadial.RadialPattern[i];
            if (!cell.InBounds(map)) continue;

            List<Thing> things = cell.GetThingList(map);
            for (int j = 0; j < things.Count; j++)
            {
                if (things[j] is Pawn p)
                {
                    if (PHRaidTargetUtility.IsDownedTarget(p, lordJob)) return p;
                }
                else if (closestCorpse == null && things[j] is Corpse corpse
                         && PHRaidTargetUtility.IsCorpseTarget(corpse, lordJob))
                {
                    closestCorpse = corpse;
                }
            }
        }
        return closestCorpse;
    }
}
