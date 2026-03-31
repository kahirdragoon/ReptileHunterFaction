using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

public class LordJob_RHF_KidnappingRaid : LordJob
{
    // kidnapper → target
    public Dictionary<Pawn, Pawn> activeKidnaps = [];

    // Scribe working lists — must be fields, not locals.
    private List<Pawn> kidnappersWorkingList = [];
    private List<Pawn> targetsWorkingList    = [];

    public override bool GuiltyOnDowned => true;

    public override StateGraph CreateGraph()
    {
        StateGraph graph = new StateGraph();

        LordToil_RHF_Assault toil_assault = new LordToil_RHF_Assault();
        LordToil_ExitMap     toil_retreat = new LordToil_ExitMap(LocomotionUrgency.Jog);

        graph.StartingToil = toil_assault; // also registers at index 0
        graph.AddToil(toil_retreat);

        Transition toRetreat = new Transition(toil_assault, toil_retreat);
        toRetreat.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
        graph.AddTransition(toRetreat);

        return graph;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    public bool IsKidnapper(Pawn p)   => activeKidnaps.ContainsKey(p);
    public bool IsTargeted(Pawn p)    => activeKidnaps.ContainsValue(p);
    public Pawn? GetTargetFor(Pawn p) => activeKidnaps.TryGetValue(p, out Pawn t) ? t : null;

    // ── Assignment ──────────────────────────────────────────────────────────

    /// <summary>
    /// Assigns <paramref name="kidnapper"/> to abduct <paramref name="downedTarget"/>.
    /// Returns false if either is already committed.
    /// </summary>
    public bool TryAssignKidnapper(Pawn downedTarget, Pawn kidnapper)
    {
        if (activeKidnaps.ContainsKey(kidnapper))   return false;
        if (activeKidnaps.ContainsValue(downedTarget)) return false;

        activeKidnaps[kidnapper] = downedTarget;
        kidnapper.mindState.duty = new PawnDuty(ReptileHunterFactionDefOf.RHF_KidnaperDuty);
        kidnapper.jobs.EndCurrentJob(JobCondition.InterruptForced);
        return true;
    }

    /// <summary>
    /// Called when a kidnap finishes (success, failure, or kidnapper lost).
    /// </summary>
    public void OnKidnapComplete(Pawn kidnapper) => activeKidnaps.Remove(kidnapper);

    // ── Lord callbacks ───────────────────────────────────────────────────────

    public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
    {
        base.Notify_PawnLost(p, condition);
        activeKidnaps.Remove(p);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(
            ref activeKidnaps,
            "activeKidnaps",
            LookMode.Reference,
            LookMode.Reference,
            ref kidnappersWorkingList,
            ref targetsWorkingList);
    }
}
