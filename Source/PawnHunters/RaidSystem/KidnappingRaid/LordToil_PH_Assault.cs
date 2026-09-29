using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnHunters;

/// <summary>
/// Main assault toil for the kidnapping raid.
/// Every 60 ticks it:
///   1. Validates active kidnap pairs (target recovered / kidnapper lost).
///   2. Assigns new kidnappers using two triggers per raider:
///      a. IMMEDIATE  — a downed player pawn is within ImmediateKidnapRange tiles.
///      b. SAFE       — the raider hasn't been harmed for SafeAfterHarmTicks and
///                      there is any untargeted downed player pawn on the map.
///   3. Sends low-HP raiders to retreat individually.
/// Multiple simultaneous kidnappings are supported.
/// </summary>
public class LordToil_PH_Assault : LordToil
{
    private const int   ImmediateKidnapRange       = 3;
    private const int   SafeAfterHarmTicks         = 300;
    private const float IndividualRetreatHpFraction = 0.33f;
    private const int   TickInterval               = 60;

    // Per-tick buffers, reused so the 60-tick scans don't allocate.
    private readonly List<Pawn> _targets   = [];
    private readonly List<Pawn> _prisoners = [];
    private readonly List<Pawn> _colonists = [];
    private static readonly List<Pawn> tmpPawns = [];

    public override void UpdateAllDuties()
    {
        var lordJob = (LordJob_PH_KidnappingRaid)lord.LordJob;

        foreach (Pawn p in lord.ownedPawns)
        {
            // Active kidnappers and skull extractors keep their duty.
            if (lordJob.IsKidnapper(p) && lordJob.GetTargetFor(p) != null) continue;
            if (lordJob.IsSkullExtractor(p)) continue;

            p.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
        }
    }

    public override void LordToilTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0) return;

        var lordJob = (LordJob_PH_KidnappingRaid)lord.LordJob;
        Map map = lord.Map;

        // --- 1. Validate active kidnap pairs ---
        ValidateKidnaps(lordJob);

        // --- 2. Assign new kidnappers ---
        TryFindAndAssignKidnappers(lordJob, map);

        // --- 2b. Prioritize attacking eligible standing prisoners when closer ---
        TryPrioritizePrisonerAttacks(lordJob, map);

        // --- 3. Skull extraction management ---
        ManageSkullExtractors(lordJob);

        // --- 4. Individual retreat for low-HP pawns ---
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

    private static void ValidateKidnaps(LordJob_PH_KidnappingRaid lordJob)
    {
        if (lordJob.activeKidnaps.Count == 0) return;

        tmpPawns.Clear();
        foreach (var kvp in lordJob.activeKidnaps)
        {
            Pawn kidnapper = kvp.Key;
            Pawn target    = kvp.Value;

            bool carried = !kidnapper.Dead && !kidnapper.Downed
                           && kidnapper.carryTracker.CarriedThing == target;

            if (target.Dead || (!target.Downed && !carried))
                tmpPawns.Add(kidnapper);
        }

        foreach (Pawn k in tmpPawns)
        {
            lordJob.OnKidnapComplete(k);
            if (!k.Dead && !k.Downed)
                lordJob.ResetToFreeDuty(k);
        }
        tmpPawns.Clear();
    }

    private void TryFindAndAssignKidnappers(LordJob_PH_KidnappingRaid lordJob, Map map)
    {
        // Collect untargeted downed eligible pawns (colonists, slaves, and prisoners).
        PHRaidTargetUtility.CollectDownedTargets(map, lordJob, _targets);
        if (_targets.Count == 0) return;

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (_targets.Count == 0) break;
            if (raider.Dead || raider.Downed) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (raider.mindState.duty?.def == DutyDefOf.ExitMapBest) continue;
            if (lordJob.IsKidnapper(raider)) continue;

            // (a) Immediate: closest downed pawn within range.
            Pawn? immediateTarget = PHRaidTargetUtility.Closest(raider.Position, _targets, ImmediateKidnapRange);
            if (immediateTarget != null)
            {
                if (lordJob.TryAssignKidnapper(immediateTarget, raider))
                    _targets.Remove(immediateTarget);
                continue;
            }

            // (b) Safe: not harmed recently → go for nearest downed pawn.
            bool isSafe = Find.TickManager.TicksGame - raider.mindState.lastHarmTick > SafeAfterHarmTicks;
            if (isSafe)
            {
                Pawn? nearest = PHRaidTargetUtility.Closest(raider.Position, _targets);
                if (nearest != null && lordJob.TryAssignKidnapper(nearest, raider))
                    _targets.Remove(nearest);
            }
        }
    }

    private void TryPrioritizePrisonerAttacks(LordJob_PH_KidnappingRaid lordJob, Map map)
    {
        // Collect standing (alive, not downed) eligible prisoners.
        PHRaidTargetUtility.CollectStandingPrisoners(map, _prisoners);
        if (_prisoners.Count == 0) return;
        PHRaidTargetUtility.CollectStandingColonists(map, _colonists);

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (raider.Dead || raider.Downed) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (raider.mindState.duty?.def == DutyDefOf.ExitMapBest) continue;
            if (lordJob.IsKidnapper(raider)) continue;
            if (lordJob.IsSkullExtractor(raider)) continue;

            Pawn? nearestPrisoner = PHRaidTargetUtility.Closest(raider.Position, _prisoners);
            if (nearestPrisoner == null) continue;

            // Only redirect if the prisoner is closer than the nearest standing colonist.
            Pawn? nearestColonist = PHRaidTargetUtility.Closest(raider.Position, _colonists);
            if (nearestColonist == null
                || nearestPrisoner.Position.DistanceToSquared(raider.Position)
                   < nearestColonist.Position.DistanceToSquared(raider.Position))
                raider.mindState.enemyTarget = nearestPrisoner;
        }
    }

    private void ManageSkullExtractors(LordJob_PH_KidnappingRaid lordJob)
    {
        // Detect skulls extracted since last tick (vanilla driver removes the head body part).
        foreach (var kvp in lordJob.pendingSkullTargets)
        {
            tmpPawns.Clear();
            foreach (Pawn victim in kvp.Value)
            {
                if (!victim.health.hediffSet.HasHead)
                {
                    WorldComp_SpoilsOfBattle.Get()?.AddSkull(victim.LabelShort);
                    tmpPawns.Add(victim);
                }
            }
            foreach (Pawn v in tmpPawns)
                lordJob.OnSkullExtracted(kvp.Key, v);
        }

        // Finish extractors who have no more valid corpses to loot.
        tmpPawns.Clear();
        foreach (Pawn p in lord.ownedPawns)
        {
            if (!lordJob.IsSkullExtractor(p) || p.Dead || p.Downed) continue;
            if (lordJob.NextSkullTarget(p) == null)
                tmpPawns.Add(p);
        }
        foreach (Pawn p in tmpPawns)
            lordJob.FinishSkullExtraction(p);
        tmpPawns.Clear();

        // Assign extraction to safe raiders who have pending kills.
        foreach (Pawn raider in lord.ownedPawns)
        {
            if (raider.Dead || raider.Downed) continue;
            if (raider.mindState.duty?.def == DutyDefOf.ExitMapBest) continue;
            if (lordJob.IsSkullExtractor(raider)) continue;
            if (!lordJob.HasPendingSkulls(raider)) continue;

            bool isSafe = Find.TickManager.TicksGame - raider.mindState.lastHarmTick > SafeAfterHarmTicks;
            if (isSafe)
                lordJob.StartSkullExtraction(raider);
        }

        // Catches designations whose extractor state was lost on load.
        lordJob.RemoveStaleSkullDesignations();
    }
}
