using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

public class LordJob_RHF_KidnappingRaidBig : LordJob, IKidnappingLordJob
{
    // ── Kidnapping ───────────────────────────────────────────────────────────

    // kidnapper → downed target (serialized)
    public Dictionary<Pawn, Pawn> activeKidnaps = [];

    private List<Pawn> kidnappersWorkingList       = [];
    private List<Pawn> kidnappersTargetsWorkingList = [];

    // ── Corpse carrying ──────────────────────────────────────────────────────

    // carrier → corpse (serialized)
    public Dictionary<Pawn, Corpse> activeCorpseCarriers = [];

    private List<Pawn>   carrierKeyWorkingList    = [];
    private List<Corpse> carrierCorpseWorkingList = [];

    // ── Transient state (not serialized) ────────────────────────────────────

    public bool massKidnapMode         = false;
    public int  initialPlayerPawnCount = 0;  // set on first tick; re-set after load

    // ── LordJob overrides ────────────────────────────────────────────────────

    public override bool GuiltyOnDowned => true;

    public override StateGraph CreateGraph()
    {
        StateGraph graph = new StateGraph();

        LordToil_RHF_AssaultBig       toil_assault = new LordToil_RHF_AssaultBig();
        LordToil_RHF_RetreatWithCarry toil_retreat = new LordToil_RHF_RetreatWithCarry();

        graph.StartingToil = toil_assault;
        graph.AddToil(toil_retreat);

        Transition toRetreat = new Transition(toil_assault, toil_retreat);
        toRetreat.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
        toRetreat.AddTrigger(new Trigger_TicksPassed(Rand.Range(26000, 38000)));
        graph.AddTransition(toRetreat);

        return graph;
    }

    // ── Kidnapping helpers ───────────────────────────────────────────────────

    public bool  IsKidnapper(Pawn p)  => activeKidnaps.ContainsKey(p);
    public bool  IsTargeted(Pawn p)   => activeKidnaps.ContainsValue(p);
    public Pawn? GetTargetFor(Pawn p) => activeKidnaps.TryGetValue(p, out Pawn t) ? t : null;

    public bool TryAssignKidnapper(Pawn downedTarget, Pawn kidnapper)
    {
        if (activeKidnaps.ContainsKey(kidnapper))      return false;
        if (activeKidnaps.ContainsValue(downedTarget)) return false;

        activeKidnaps[kidnapper] = downedTarget;
        kidnapper.mindState.duty = new PawnDuty(ReptileHunterFactionDefOf.RHF_KidnaperDuty_Big);
        kidnapper.jobs.EndCurrentJob(JobCondition.InterruptForced);
        return true;
    }

    public void OnKidnapComplete(Pawn kidnapper) => activeKidnaps.Remove(kidnapper);

    // ── Corpse carrier helpers ───────────────────────────────────────────────

    public bool    IsCorpseCarrier(Pawn p)   => activeCorpseCarriers.ContainsKey(p);
    public bool    IsCorpseTargeted(Corpse c) => activeCorpseCarriers.ContainsValue(c);
    public Corpse? GetCorpseFor(Pawn p)      =>
        activeCorpseCarriers.TryGetValue(p, out Corpse c) ? c : null;

    public bool TryAssignCorpseCarrier(Corpse corpse, Pawn carrier)
    {
        if (activeCorpseCarriers.ContainsKey(carrier))  return false;
        if (activeCorpseCarriers.ContainsValue(corpse)) return false;

        activeCorpseCarriers[carrier] = corpse;
        carrier.mindState.duty = new PawnDuty(ReptileHunterFactionDefOf.RHF_CarryCorpseDuty);
        carrier.jobs.EndCurrentJob(JobCondition.InterruptForced);
        return true;
    }

    public void OnCorpseCarryComplete(Pawn carrier) => activeCorpseCarriers.Remove(carrier);

    // ── Shared validation (called from both toils) ───────────────────────────

    /// <param name="inRetreat">
    /// When false (assault phase), frees kidnapper is snapped back to AssaultColony.
    /// When true (retreat phase), let UpdateAllDuties handle duty reassignment.
    /// </param>
    public void ValidateKidnaps(bool inRetreat = false)
    {
        var toRemove = new List<Pawn>();

        foreach (var kvp in activeKidnaps)
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
            OnKidnapComplete(k);
            if (!inRetreat && !k.Dead && !k.Downed)
                k.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
        }
    }

    public void ValidateCorpseCarriers()
    {
        var toRemove = new List<Pawn>();

        foreach (var kvp in activeCorpseCarriers)
        {
            Pawn   carrier = kvp.Key;
            Corpse corpse  = kvp.Value;

            bool carried = !carrier.Dead && !carrier.Downed
                           && carrier.carryTracker.CarriedThing == corpse;

            if (!corpse.Spawned && !carried)
                toRemove.Add(carrier);
        }

        foreach (Pawn c in toRemove)
            OnCorpseCarryComplete(c);
    }

    // ── Lord callbacks ───────────────────────────────────────────────────────

    public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
    {
        base.Notify_PawnLost(p, condition);
        activeKidnaps.Remove(p);
        activeCorpseCarriers.Remove(p);
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
            ref kidnappersTargetsWorkingList);
        Scribe_Collections.Look(
            ref activeCorpseCarriers,
            "activeCorpseCarriers",
            LookMode.Reference,
            LookMode.Reference,
            ref carrierKeyWorkingList,
            ref carrierCorpseWorkingList);
        // massKidnapMode and initialPlayerPawnCount are intentionally not serialized.
        // They are re-evaluated from map state on the first tick after load.
    }
}
