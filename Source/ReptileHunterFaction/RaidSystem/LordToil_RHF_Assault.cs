using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

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
public class LordToil_RHF_Assault : LordToil
{
    private const int   ImmediateKidnapRange       = 3;
    private const int   SafeAfterHarmTicks         = 300;
    private const float IndividualRetreatHpFraction = 0.33f;
    private const int   TickInterval               = 60;

    public override void UpdateAllDuties()
    {
        var lordJob = (LordJob_RHF_KidnappingRaid)lord.LordJob;

        foreach (Pawn p in lord.ownedPawns)
        {
            // Active kidnappers keep their duty; everyone else assaults.
            if (lordJob.IsKidnapper(p) && lordJob.GetTargetFor(p) != null)
                continue;

            p.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
        }
    }

    public override void LordToilTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0) return;

        var lordJob = (LordJob_RHF_KidnappingRaid)lord.LordJob;
        Map map = lord.Map;

        // --- 1. Validate active kidnap pairs ---
        ValidateKidnaps(lordJob);

        // --- 2. Assign new kidnappers ---
        TryFindAndAssignKidnappers(lordJob, map);

        // --- 3. Individual retreat for low-HP pawns ---
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

    private static void ValidateKidnaps(LordJob_RHF_KidnappingRaid lordJob)
    {
        var toRemove = new List<Pawn>();

        foreach (var kvp in lordJob.activeKidnaps)
        {
            Pawn kidnapper = kvp.Key;
            Pawn target    = kvp.Value;

            bool carried = !kidnapper.Dead && !kidnapper.Downed
                           && kidnapper.carryTracker.CarriedThing == target;

            if (target.Dead || (!target.Downed && !carried))
                toRemove.Add(kidnapper);
        }

        foreach (Pawn k in toRemove)
        {
            lordJob.OnKidnapComplete(k);
            if (!k.Dead && !k.Downed)
                k.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
        }
    }

    private void TryFindAndAssignKidnappers(LordJob_RHF_KidnappingRaid lordJob, Map map)
    {
        // Collect untargeted downed player pawns.
        var availableTargets = new List<Pawn>();
        foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
        {
            if (!p.Dead && p.Downed && p.RaceProps.Humanlike
                && p.Faction == Faction.OfPlayer && !lordJob.IsTargeted(p))
                availableTargets.Add(p);
        }
        if (availableTargets.Count == 0) return;

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (availableTargets.Count == 0) break;
            if (raider.Dead || raider.Downed) continue;
            if (!raider.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
            if (raider.mindState.duty?.def == DutyDefOf.ExitMapBest) continue;
            if (lordJob.IsKidnapper(raider)) continue;

            // (a) Immediate: closest downed pawn within range.
            Pawn? immediateTarget = ClosestWithinRange(raider, availableTargets, ImmediateKidnapRange);
            if (immediateTarget != null)
            {
                if (lordJob.TryAssignKidnapper(immediateTarget, raider))
                    availableTargets.Remove(immediateTarget);
                continue;
            }

            // (b) Safe: not harmed recently → go for nearest downed pawn.
            bool isSafe = Find.TickManager.TicksGame - raider.mindState.lastHarmTick > SafeAfterHarmTicks;
            if (isSafe)
            {
                Pawn? nearest = Nearest(raider, availableTargets);
                if (nearest != null && lordJob.TryAssignKidnapper(nearest, raider))
                    availableTargets.Remove(nearest);
            }
        }
    }

    private static Pawn? ClosestWithinRange(Pawn raider, List<Pawn> candidates, int range)
    {
        Pawn? best     = null;
        float bestDist = float.MaxValue;

        foreach (Pawn p in candidates)
        {
            float d = p.Position.DistanceTo(raider.Position);
            if (d <= range && d < bestDist)
            {
                bestDist = d;
                best     = p;
            }
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
}
