using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnHunters;

public class LordJob_PH_KidnappingRaid : LordJob, IKidnappingLordJob
{
    // ── Kidnapping ───────────────────────────────────────────────────────────

    // kidnapper → target (serialized)
    public Dictionary<Pawn, Pawn> activeKidnaps = [];

    private List<Pawn> kidnappersWorkingList = [];
    private List<Pawn> targetsWorkingList    = [];

    // ── Skull extraction ─────────────────────────────────────────────────────

    // raider → list of killed player pawns whose skulls are pending
    // Not serialized — resets on load; WorldComp_SpoilsOfBattle is the persistent store.
    public Dictionary<Pawn, List<Pawn>> pendingSkullTargets = [];
    public HashSet<Pawn>                activeSkullExtractors = [];

    // Corpses this raid put an ExtractSkull designation on (serialized, so they can be cleaned up after a load).
    // The designation is player-visible, so a leftover one would let the player's own pawns take the skull.
    private HashSet<Corpse> designatedCorpses = [];

    // ── LordJob overrides ────────────────────────────────────────────────────

    public override bool GuiltyOnDowned => true;

    public override StateGraph CreateGraph()
    {
        StateGraph graph = new StateGraph();

        LordToil_PH_Assault toil_assault = new LordToil_PH_Assault();
        LordToil_ExitMap     toil_retreat = new LordToil_ExitMap(LocomotionUrgency.Jog);

        graph.StartingToil = toil_assault; // also registers at index 0
        graph.AddToil(toil_retreat);

        Transition toRetreat = new Transition(toil_assault, toil_retreat);
        toRetreat.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
        // Everyone leaves now, so no skull extraction will happen anymore.
        toRetreat.AddPreAction(new TransitionAction_Custom(() => RemoveStaleSkullDesignations(all: true)));
        graph.AddTransition(toRetreat);

        return graph;
    }

    /// <summary>Assault toil: back to attacking. Retreat toil: the same exit duty vanilla's LordToil_ExitMap gives everyone.</summary>
    public void ResetToFreeDuty(Pawn p) =>
        p.mindState.duty = lord.CurLordToil is LordToil_PH_Assault
            ? new PawnDuty(DutyDefOf.AssaultColony)
            : new PawnDuty(DutyDefOf.ExitMapBest) { locomotion = LocomotionUrgency.Jog };

    // ── Kidnapping helpers ───────────────────────────────────────────────────

    public bool  IsKidnapper(Pawn p)   => activeKidnaps.ContainsKey(p);
    public bool  IsTargeted(Pawn p)    => activeKidnaps.ContainsValue(p);
    public Pawn? GetTargetFor(Pawn p)  => activeKidnaps.TryGetValue(p, out Pawn t) ? t : null;

    public bool TryAssignKidnapper(Pawn downedTarget, Pawn kidnapper)
    {
        if (activeKidnaps.ContainsKey(kidnapper))      return false;
        if (activeKidnaps.ContainsValue(downedTarget)) return false;

        activeKidnaps[kidnapper] = downedTarget;
        kidnapper.mindState.duty = new PawnDuty(PawnHuntersDefOf.PH_KidnaperDuty);
        kidnapper.jobs.EndCurrentJob(JobCondition.InterruptForced);
        return true;
    }

    public void OnKidnapComplete(Pawn kidnapper) => activeKidnaps.Remove(kidnapper);

    // ── Skull extraction helpers ─────────────────────────────────────────────

    public bool IsSkullExtractor(Pawn p) => activeSkullExtractors.Contains(p);

    public bool HasPendingSkulls(Pawn raider) =>
        pendingSkullTargets.TryGetValue(raider, out var list) && list.Count > 0;

    /// <summary>Records a kill for later skull extraction.</summary>
    public void RegisterKill(Pawn killer, Pawn victim)
    {
        if(!victim.health.hediffSet.HasHead) return; // no skull to extract
        
        if (!pendingSkullTargets.TryGetValue(killer, out var list))
        {
            list = [];
            pendingSkullTargets[killer] = list;
        }
        if (!list.Contains(victim))
            list.Add(victim);
    }

    /// <summary>
    /// Returns the next victim whose corpse is still valid on this map
    /// AND whose skull has not yet been extracted (head still present).
    /// </summary>
    public Pawn? NextSkullTarget(Pawn extractor)
    {
        if (!pendingSkullTargets.TryGetValue(extractor, out var list)) return null;
        foreach (Pawn victim in list)
        {
            Corpse? corpse = victim.Corpse;
            if (corpse != null && corpse.Spawned && corpse.Map == lord.Map
                && victim.health.hediffSet.HasHead) // not yet extracted
                return victim;
        }
        return null;
    }

    /// <summary>Removes a victim from the extractor's pending list.</summary>
    public void OnSkullExtracted(Pawn extractor, Pawn victim)
    {
        if (pendingSkullTargets.TryGetValue(extractor, out var list))
            list.Remove(victim);
    }

    /// <summary>Switches raider to skull-extractor duty.</summary>
    public void StartSkullExtraction(Pawn raider)
    {
        activeSkullExtractors.Add(raider);
        raider.mindState.duty = new PawnDuty(PawnHuntersDefOf.PH_SkullExtractorDuty);
        raider.jobs.EndCurrentJob(JobCondition.InterruptForced);
    }

    /// <summary>Skull extraction done — raider retreats.</summary>
    public void FinishSkullExtraction(Pawn raider)
    {
        activeSkullExtractors.Remove(raider);
        pendingSkullTargets.Remove(raider);
        RemoveStaleSkullDesignations();
        raider.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest);
        raider.jobs.EndCurrentJob(JobCondition.InterruptForced);
    }

    /// <summary>
    /// The vanilla JobDriver_ExtractSkull fails immediately without an ExtractSkull designation on the corpse,
    /// so add one and remember it. A corpse the player already designated is left alone.
    /// </summary>
    public void DesignateSkullExtraction(Corpse corpse)
    {
        DesignationManager designations = corpse.Map.designationManager;
        if (designations.DesignationOn(corpse, DesignationDefOf.ExtractSkull) != null) return;
        designations.AddDesignation(new Designation(corpse, DesignationDefOf.ExtractSkull));
        designatedCorpses.Add(corpse);
    }

    /// <summary>
    /// Removes our ExtractSkull designations from corpses no active skull extractor is still going for
    /// (extractor killed, left, or state lost on load). <paramref name="all"/> removes every one of them.
    /// </summary>
    public void RemoveStaleSkullDesignations(bool all = false)
    {
        designatedCorpses.RemoveWhere(corpse =>
        {
            if (!all && !corpse.Destroyed && IsPendingSkull(corpse.InnerPawn)) return false;
            if (corpse.Spawned)
                corpse.Map.designationManager.TryRemoveDesignationOn(corpse, DesignationDefOf.ExtractSkull);
            return true;
        });
    }

    private bool IsPendingSkull(Pawn victim)
    {
        foreach (Pawn extractor in activeSkullExtractors)
        {
            if (pendingSkullTargets.TryGetValue(extractor, out var list) && list.Contains(victim))
                return true;
        }
        return false;
    }

    // ── Lord callbacks ───────────────────────────────────────────────────────

    public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
    {
        base.Notify_PawnLost(p, condition);
        activeKidnaps.Remove(p);
        pendingSkullTargets.Remove(p);
        activeSkullExtractors.Remove(p);
        RemoveStaleSkullDesignations();
    }

    public override void Cleanup()
    {
        base.Cleanup();
        RemoveStaleSkullDesignations(all: true);
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
        // pendingSkullTargets / activeSkullExtractors intentionally not saved —
        // WorldComp_SpoilsOfBattle holds the persistent record. Orphaned extractors go back to the free duty
        // (JobGiver_PH_ExtractSkull) and their designations are removed by RemoveStaleSkullDesignations.
        Scribe_Collections.Look(ref designatedCorpses, "designatedCorpses", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            designatedCorpses ??= [];
            designatedCorpses.RemoveWhere(c => c == null);
        }
    }
}
