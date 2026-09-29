using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnHunters;

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
public class LordToil_PH_AssaultBig : LordToil
{
    private const int   ImmediateKidnapRange        = 3;
    private const int   SafeAfterHarmTicks          = 300;
    private const float MassKidnapPlayerPawnFraction = 0.5f;
    private const float IndividualRetreatHpFraction  = 0.33f;
    private const int   TickInterval                = 60;

    // Per-tick buffers, reused so the 60-tick scans don't allocate.
    private readonly List<Pawn>   _targets   = [];
    private readonly List<Pawn>   _prisoners = [];
    private readonly List<Pawn>   _colonists = [];
    private readonly List<Corpse> _corpses   = [];

    public override void UpdateAllDuties()
    {
        var lordJob = (LordJob_PH_KidnappingRaidBig)lord.LordJob;

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

        var lordJob = (LordJob_PH_KidnappingRaidBig)lord.LordJob;
        Map map     = lord.Map;

        // 1. Initialise initial player pawn count on first eligible tick.
        if (lordJob.initialPlayerPawnCount == 0)
            lordJob.initialPlayerPawnCount = CountPlayerCombatPawns(map, healthyOnly: false);

        // 2. Validate existing assignments.
        lordJob.ValidateKidnaps();
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

    /// <summary>
    /// Free colonists plus adult slaves. FreeColonistsSpawned already includes slaves (a slave's HostFaction
    /// is null), so child slaves are filtered out instead of adding the slave list again. The baseline and
    /// the healthy count must use the same population, or the mass-kidnap fraction is skewed.
    /// </summary>
    private static int CountPlayerCombatPawns(Map map, bool healthyOnly)
    {
        int count = 0;
        foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
        {
            if (p.IsSlave && !p.DevelopmentalStage.Adult()) continue;
            if (healthyOnly && p.Downed) continue;
            count++;
        }
        return count;
    }

    // ── Mass-kidnap trigger ──────────────────────────────────────────────────

    private void CheckMassKidnapTrigger(LordJob_PH_KidnappingRaidBig lordJob, Map map)
    {
        if (lordJob.massKidnapMode) return;
        if (lordJob.initialPlayerPawnCount <= 0) return;

        // Count currently healthy player pawns (same population as the baseline; the dead are no longer spawned).
        int currentlyHealthy = CountPlayerCombatPawns(map, healthyOnly: true);

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

    private void TryFindAndAssignKidnappers(LordJob_PH_KidnappingRaidBig lordJob, Map map)
    {
        // Build target list: prisoners first (priority), then free colonists/slaves
        // only when no qualifying prisoners exist at all.
        PHRaidTargetUtility.CollectDownedTargets(map, lordJob, _targets, playerPawns: false);
        if (_targets.Count == 0)
            PHRaidTargetUtility.CollectDownedTargets(map, lordJob, _targets, prisoners: false);

        if (_targets.Count == 0) return;

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (_targets.Count == 0) break;
            if (raider.Dead || raider.Downed) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (raider.mindState.duty?.def == DutyDefOf.ExitMapBest) continue;
            if (lordJob.IsKidnapper(raider)) continue;

            // (a) Immediate: downed pawn within 3 tiles.
            Pawn? immediateTarget = PHRaidTargetUtility.Closest(raider.Position, _targets, ImmediateKidnapRange);
            if (immediateTarget != null)
            {
                if (lordJob.TryAssignKidnapper(immediateTarget, raider))
                    _targets.Remove(immediateTarget);
                continue;
            }

            // (b) Safe: not harmed recently → take nearest downed pawn.
            bool isSafe = Find.TickManager.TicksGame - raider.mindState.lastHarmTick > SafeAfterHarmTicks;
            if (isSafe)
            {
                Pawn? nearest = PHRaidTargetUtility.Closest(raider.Position, _targets);
                if (nearest != null && lordJob.TryAssignKidnapper(nearest, raider))
                    _targets.Remove(nearest);
            }
        }
    }

    // ── Prisoner attack prioritization ──────────────────────────────────────

    private void TryPrioritizePrisonerAttacks(LordJob_PH_KidnappingRaidBig lordJob, Map map)
    {
        PHRaidTargetUtility.CollectStandingPrisoners(map, _prisoners);
        if (_prisoners.Count == 0) return;
        PHRaidTargetUtility.CollectStandingColonists(map, _colonists);

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (raider.Dead || raider.Downed) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (raider.mindState.duty?.def == DutyDefOf.ExitMapBest) continue;
            if (lordJob.IsKidnapper(raider) || lordJob.IsCorpseCarrier(raider)) continue;

            Pawn? nearestPrisoner = PHRaidTargetUtility.Closest(raider.Position, _prisoners);
            if (nearestPrisoner == null) continue;

            Pawn? nearestColonist = PHRaidTargetUtility.Closest(raider.Position, _colonists);
            if (nearestColonist == null
                || nearestPrisoner.Position.DistanceToSquared(raider.Position)
                   < nearestColonist.Position.DistanceToSquared(raider.Position))
                raider.mindState.enemyTarget = nearestPrisoner;
        }
    }

    // ── Mass-kidnap assignment ───────────────────────────────────────────────

    private void TryAssignMassKidnapping(LordJob_PH_KidnappingRaidBig lordJob, Map map)
    {
        // Downed qualifying player pawns (highest priority).
        PHRaidTargetUtility.CollectDownedTargets(map, lordJob, _targets);

        // Player pawn corpses (secondary). Collected only once a raider is left over after the downed pawns,
        // since the corpse group holds every corpse on the map (animals included).
        bool corpsesCollected = false;

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (raider.Dead || raider.Downed) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (lordJob.IsKidnapper(raider) || lordJob.IsCorpseCarrier(raider)) continue;

            if (_targets.Count > 0)
            {
                Pawn? nearest = PHRaidTargetUtility.Closest(raider.Position, _targets);
                if (nearest != null && lordJob.TryAssignKidnapper(nearest, raider))
                    _targets.Remove(nearest);
                continue;
            }

            if (!corpsesCollected)
            {
                PHRaidTargetUtility.CollectCorpseTargets(map, lordJob, _corpses);
                corpsesCollected = true;
            }
            if (_corpses.Count == 0) break;

            Corpse? nearestCorpse = PHRaidTargetUtility.Closest(raider.Position, _corpses);
            if (nearestCorpse != null && lordJob.TryAssignCorpseCarrier(nearestCorpse, raider))
                _corpses.Remove(nearestCorpse);
        }
    }
}
