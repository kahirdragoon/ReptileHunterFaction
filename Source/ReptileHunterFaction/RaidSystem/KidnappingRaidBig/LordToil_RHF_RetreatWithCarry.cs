using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Retreat toil for the big kidnapping raid.
/// Free raiders flee using ExitMapBestAndDefendSelf.
/// Active kidnappers finish carrying their target off map.
/// Active corpse carriers finish carrying their corpse off map.
/// Each tick, free retreating raiders within 2 tiles of a downed player pawn or
/// player corpse opportunistically grab it before leaving.
/// </summary>
public class LordToil_RHF_RetreatWithCarry : LordToil
{
    private const int OpportunisticGrabRange = 2;
    private const int TickInterval           = 60;

    public override void Init()
    {
        base.Init();
        UpdateAllDuties();
    }

    public override void UpdateAllDuties()
    {
        var lordJob = (LordJob_RHF_KidnappingRaidBig)lord.LordJob;

        foreach (Pawn p in lord.ownedPawns)
        {
            if (lordJob.IsKidnapper(p) && lordJob.GetTargetFor(p) != null)
            {
                p.mindState.duty = new PawnDuty(ReptileHunterFactionDefOf.RHF_KidnaperDuty_Big);
            }
            else if (lordJob.IsCorpseCarrier(p))
            {
                p.mindState.duty = new PawnDuty(ReptileHunterFactionDefOf.RHF_CarryCorpseDuty);
            }
            else
            {
                p.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBestAndDefendSelf);
                p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }

    public override void LordToilTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0) return;

        var lordJob = (LordJob_RHF_KidnappingRaidBig)lord.LordJob;
        Map map = lord.Map;

        lordJob.ValidateKidnaps(inRetreat: true);
        lordJob.ValidateCorpseCarriers();

        TryOpportunisticGrab(lordJob, map);
    }

    private void TryOpportunisticGrab(LordJob_RHF_KidnappingRaidBig lordJob, Map map)
    {
        bool dutiesChanged = false;

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (raider.Dead || raider.Downed) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (lordJob.IsKidnapper(raider) || lordJob.IsCorpseCarrier(raider)) continue;

            // Priority 1: downed player pawn within 2 tiles
            Pawn? nearbyDowned = ClosestDownedPlayerPawnWithinRange(
                raider, map, OpportunisticGrabRange, lordJob);
            if (nearbyDowned != null)
            {
                if (lordJob.TryAssignKidnapper(nearbyDowned, raider))
                    dutiesChanged = true;
                continue;
            }

            // Priority 2: player corpse within 2 tiles
            Corpse? nearbyCorpse = ClosestPlayerCorpseWithinRange(
                raider, map, OpportunisticGrabRange, lordJob);
            if (nearbyCorpse != null && lordJob.TryAssignCorpseCarrier(nearbyCorpse, raider))
                dutiesChanged = true;
        }

        if (dutiesChanged)
            UpdateAllDuties();
    }

    private static Pawn? ClosestDownedPlayerPawnWithinRange(
        Pawn raider, Map map, int range, LordJob_RHF_KidnappingRaidBig lordJob)
    {
        Pawn? best     = null;
        float bestDist = float.MaxValue;

        foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
        {
            if (p.Dead || !p.Downed || !p.RaceProps.Humanlike) continue;
            if (p.Faction != Faction.OfPlayer && !p.IsPrisonerOfColony) continue;
            if (lordJob.IsTargeted(p)) continue;
            if (!RHFPawnTargetingUtility.IsTargetPawn(p)) continue;

            float d = p.Position.DistanceTo(raider.Position);
            if (d <= range && d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    private static Corpse? ClosestPlayerCorpseWithinRange(
        Pawn raider, Map map, int range, LordJob_RHF_KidnappingRaidBig lordJob)
    {
        Corpse? best     = null;
        float   bestDist = float.MaxValue;

        foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
        {
            if (t is not Corpse corpse) continue;
            if (corpse.InnerPawn?.Faction != Faction.OfPlayer) continue;
            if (!corpse.InnerPawn.RaceProps.Humanlike) continue;
            if (lordJob.IsCorpseTargeted(corpse)) continue;
            if (!RHFPawnTargetingUtility.IsTargetPawn(corpse.InnerPawn)) continue;

            float d = corpse.Position.DistanceTo(raider.Position);
            if (d <= range && d < bestDist) { bestDist = d; best = corpse; }
        }
        return best;
    }
}
