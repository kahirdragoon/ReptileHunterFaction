using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Main assault toil for the big kidnapping raid.
///
/// Every 60 ticks:
///   1. Initialises initialPlayerPawnCount on first tick (or after load).
///   2. Validates existing kidnap/corpse-carry pairs.
///   3. Checks the mass-kidnap trigger (≥50% of starting player pawns downed/dead).
///   4a. If mass-kidnap mode: assigns all free raiders to downed pawns then corpses.
///   4b. Else: assigns kidnappers to downed prisoners first (fallback: player pawns)
///       with immediate (3-tile) and safe (300-tick) triggers, and redirects raider
///       enemyTarget toward qualifying standing prisoners when they are closer than
///       the nearest standing colonist.
///   5. Individual retreat for raiders below 33 % HP.
/// </summary>
public class LordToil_RHF_AssaultBig : LordToil
{
    private const int   ImmediateKidnapRange        = 3;
    private const int   SafeAfterHarmTicks          = 300;
    private const float MassKidnapPlayerPawnFraction = 0.5f;
    private const float IndividualRetreatHpFraction  = 0.33f;
    private const int   TickInterval                = 60;

    public override void UpdateAllDuties()
    {
        var lordJob = (LordJob_RHF_KidnappingRaidBig)lord.LordJob;

        foreach (Pawn p in lord.ownedPawns)
        {
            if (lordJob.IsKidnapper(p) && lordJob.GetTargetFor(p) != null) continue;
            if (lordJob.IsCorpseCarrier(p)) continue;
            p.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
        }
    }

    public override void LordToilTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0) return;

        var lordJob = (LordJob_RHF_KidnappingRaidBig)lord.LordJob;
        Map map     = lord.Map;

        // 1. Initialise initial player pawn count on first eligible tick.
        if (lordJob.initialPlayerPawnCount == 0)
            lordJob.initialPlayerPawnCount = CountPlayerCombatPawns(map);

        // 2. Validate existing assignments.
        lordJob.ValidateKidnaps(inRetreat: false);
        lordJob.ValidateCorpseCarriers();

        // 3. Check mass-kidnap trigger.
        CheckMassKidnapTrigger(lordJob, map);

        if (lordJob.massKidnapMode)
        {
            // 4a. Mass kidnap: assign every free raider to a downed pawn or corpse.
            TryAssignMassKidnapping(lordJob, map);
        }
        else
        {
            // 4b. Normal mode.
            TryFindAndAssignKidnappers(lordJob, map);
            TryPrioritizePrisonerAttacks(lordJob, map);
        }

        // 5. Individual retreat for low-HP raiders.
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
    }

    // ── Player pawn counting ─────────────────────────────────────────────────

    private static int CountPlayerCombatPawns(Map map) =>
        map.mapPawns.FreeColonistsSpawned.Count
        + map.mapPawns.SlavesOfColonySpawned.Count(s => s.DevelopmentalStage.Adult());

    // ── Mass-kidnap trigger ──────────────────────────────────────────────────

    private void CheckMassKidnapTrigger(LordJob_RHF_KidnappingRaidBig lordJob, Map map)
    {
        if (lordJob.massKidnapMode) return;
        if (lordJob.initialPlayerPawnCount <= 0) return;

        // Count currently healthy free colonists (not downed, not dead).
        int currentlyHealthy = map.mapPawns.FreeColonistsSpawned
            .Count(p => !p.Downed && !p.Dead);

        // Effective downed/dead = initial count minus those still healthy.
        // This accounts for colonists killed and despawned (no longer in AllPawnsSpawned).
        int effectiveDownedOrDead = lordJob.initialPlayerPawnCount - currentlyHealthy;

        if (effectiveDownedOrDead < lordJob.initialPlayerPawnCount * MassKidnapPlayerPawnFraction)
            return;

        lordJob.massKidnapMode = true;

        // Interrupt all free raiders so they re-evaluate immediately.
        foreach (Pawn raider in lord.ownedPawns)
        {
            if (!raider.Dead && !raider.Downed && !lordJob.IsKidnapper(raider))
                raider.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }
    }

    // ── Normal kidnapper assignment ──────────────────────────────────────────

    private void TryFindAndAssignKidnappers(LordJob_RHF_KidnappingRaidBig lordJob, Map map)
    {
        // Build target list: prisoners first (priority), then free colonists/slaves
        // only when no qualifying prisoners exist at all.
        var availableTargets = new List<Pawn>();

        foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
        {
            if (!p.Dead && p.Downed && p.RaceProps.Humanlike
                && p.IsPrisonerOfColony && !lordJob.IsTargeted(p)
                && RHFPawnTargetingUtility.IsTargetPawn(p))
                availableTargets.Add(p);
        }

        bool hasPrisoners = availableTargets.Count > 0;
        if (!hasPrisoners)
        {
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (!p.Dead && p.Downed && p.RaceProps.Humanlike
                    && p.Faction == Faction.OfPlayer && !p.IsPrisonerOfColony
                    && !lordJob.IsTargeted(p) && RHFPawnTargetingUtility.IsTargetPawn(p))
                    availableTargets.Add(p);
            }
        }

        if (availableTargets.Count == 0) return;

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (availableTargets.Count == 0) break;
            if (raider.Dead || raider.Downed) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (raider.mindState.duty?.def == DutyDefOf.ExitMapBest) continue;
            if (lordJob.IsKidnapper(raider)) continue;

            // (a) Immediate: downed pawn within 3 tiles.
            Pawn? immediateTarget = ClosestWithinRange(raider, availableTargets, ImmediateKidnapRange);
            if (immediateTarget != null)
            {
                if (lordJob.TryAssignKidnapper(immediateTarget, raider))
                    availableTargets.Remove(immediateTarget);
                continue;
            }

            // (b) Safe: not harmed recently → take nearest downed pawn.
            bool isSafe = Find.TickManager.TicksGame - raider.mindState.lastHarmTick > SafeAfterHarmTicks;
            if (isSafe)
            {
                Pawn? nearest = Nearest(raider, availableTargets);
                if (nearest != null && lordJob.TryAssignKidnapper(nearest, raider))
                    availableTargets.Remove(nearest);
            }
        }
    }

    // ── Prisoner attack prioritization ──────────────────────────────────────

    private void TryPrioritizePrisonerAttacks(LordJob_RHF_KidnappingRaidBig lordJob, Map map)
    {
        var standingPrisoners = new List<Pawn>();
        foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
        {
            if (!p.Dead && !p.Downed && p.IsPrisonerOfColony
                && p.RaceProps.Humanlike && RHFPawnTargetingUtility.IsTargetPawn(p))
                standingPrisoners.Add(p);
        }
        if (standingPrisoners.Count == 0) return;

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (raider.Dead || raider.Downed) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (raider.mindState.duty?.def == DutyDefOf.ExitMapBest) continue;
            if (lordJob.IsKidnapper(raider) || lordJob.IsCorpseCarrier(raider)) continue;

            Pawn? nearestPrisoner = Nearest(raider, standingPrisoners);
            if (nearestPrisoner == null) continue;

            float prisonerDist = nearestPrisoner.Position.DistanceTo(raider.Position);

            float nearestColonistDist = float.MaxValue;
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.Dead || p.Downed) continue;
                float d = p.Position.DistanceTo(raider.Position);
                if (d < nearestColonistDist) nearestColonistDist = d;
            }

            if (prisonerDist < nearestColonistDist)
                raider.mindState.enemyTarget = nearestPrisoner;
        }
    }

    // ── Mass-kidnap assignment ───────────────────────────────────────────────

    private void TryAssignMassKidnapping(LordJob_RHF_KidnappingRaidBig lordJob, Map map)
    {
        // Downed qualifying player pawns (highest priority).
        var downedPawns = new List<Pawn>();
        foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
        {
            if (!p.Dead && p.Downed && p.RaceProps.Humanlike
                && (p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony)
                && !lordJob.IsTargeted(p) && RHFPawnTargetingUtility.IsTargetPawn(p))
                downedPawns.Add(p);
        }

        // Player pawn corpses (secondary).
        var availableCorpses = new List<Corpse>();
        foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
        {
            if (t is not Corpse corpse) continue;
            if (corpse.InnerPawn?.Faction != Faction.OfPlayer) continue;
            if (!corpse.InnerPawn.RaceProps.Humanlike) continue;
            if (lordJob.IsCorpseTargeted(corpse)) continue;
            if (!RHFPawnTargetingUtility.IsTargetPawn(corpse.InnerPawn)) continue;
            availableCorpses.Add(corpse);
        }

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (raider.Dead || raider.Downed) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (lordJob.IsKidnapper(raider) || lordJob.IsCorpseCarrier(raider)) continue;

            if (downedPawns.Count > 0)
            {
                Pawn? nearest = Nearest(raider, downedPawns);
                if (nearest != null && lordJob.TryAssignKidnapper(nearest, raider))
                    downedPawns.Remove(nearest);
            }
            else if (availableCorpses.Count > 0)
            {
                Corpse? nearest = NearestCorpse(raider, availableCorpses);
                if (nearest != null && lordJob.TryAssignCorpseCarrier(nearest, raider))
                    availableCorpses.Remove(nearest);
            }
        }
    }

    // ── Geometry helpers ─────────────────────────────────────────────────────

    private static Pawn? ClosestWithinRange(Pawn raider, List<Pawn> candidates, int range)
    {
        Pawn? best     = null;
        float bestDist = float.MaxValue;

        foreach (Pawn p in candidates)
        {
            float d = p.Position.DistanceTo(raider.Position);
            if (d <= range && d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    private static Pawn? Nearest(Pawn raider, List<Pawn> candidates)
    {
        Pawn? best     = null;
        float bestDist = float.MaxValue;

        foreach (Pawn p in candidates)
        {
            float d = p.Position.DistanceTo(raider.Position);
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    private static Corpse? NearestCorpse(Pawn raider, List<Corpse> candidates)
    {
        Corpse? best     = null;
        float   bestDist = float.MaxValue;

        foreach (Corpse c in candidates)
        {
            float d = c.Position.DistanceTo(raider.Position);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }
}
