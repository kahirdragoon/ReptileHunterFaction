using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

public class LordJob_RHF_ComplexLooting : LordJob
{
    // ── Room assignment ──────────────────────────────────────────────────────
    // raider → room ID they are currently exploring (serialized)
    private Dictionary<Pawn, int> _roomAssignments    = [];
    private List<Pawn>            _assignKeysWorkList  = [];
    private List<int>             _assignValsWorkList  = [];

    // room IDs that are fully explored (no crate / crate looted / threat-skipped)
    private HashSet<int> _doneRoomIDs  = [];
    private List<int>    _doneRoomList = [];

    // ── LordJob overrides ────────────────────────────────────────────────────

    public override bool GuiltyOnDowned => false;

    public override StateGraph CreateGraph()
    {
        var toil_loot    = new LordToil_RHF_ComplexLoot();
        var toil_retreat = new LordToil_ExitMap(LocomotionUrgency.Jog);

        var graph = new StateGraph();
        graph.AddToil(toil_loot);
        graph.AddToil(toil_retreat);

        var toRetreat = new Transition(toil_loot, toil_retreat);
        toRetreat.AddTrigger(new Trigger_Memo("ThreatAwakened"));
        toRetreat.AddTrigger(new Trigger_Memo("AllCratesDone"));
        graph.AddTransition(toRetreat);

        graph.StartingToil = toil_loot;
        return graph;
    }

    // ── Room assignment helpers ──────────────────────────────────────────────

    public bool TryAssignRoom(Pawn pawn, int roomID)
    {
        if (_roomAssignments.ContainsKey(pawn))      return false;
        if (_roomAssignments.ContainsValue(roomID))  return false;
        if (_doneRoomIDs.Contains(roomID))           return false;
        _roomAssignments[pawn] = roomID;
        return true;
    }

    /// <summary>Release the room without marking it done — another raider may try it.</summary>
    public void UnassignRoom(Pawn pawn) => _roomAssignments.Remove(pawn);

    /// <summary>Release the room AND mark it done — no raider will revisit.</summary>
    public void FinishRoom(Pawn pawn)
    {
        if (_roomAssignments.TryGetValue(pawn, out int id))
            _doneRoomIDs.Add(id);
        _roomAssignments.Remove(pawn);
    }

    public bool HasRoomAssignment(Pawn pawn)  => _roomAssignments.ContainsKey(pawn);
    public bool IsRoomAssigned(int roomID)    => _roomAssignments.ContainsValue(roomID);
    public bool IsRoomDone(int roomID)        => _doneRoomIDs.Contains(roomID);

    // ── Lord callbacks ───────────────────────────────────────────────────────

    public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
    {
        base.Notify_PawnLost(p, condition);
        _roomAssignments.Remove(p); // unassign but don't mark done — room can be retried
    }

    // ── Serialization ────────────────────────────────────────────────────────

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(
            ref _roomAssignments,
            "roomAssignments",
            LookMode.Reference,
            LookMode.Value,
            ref _assignKeysWorkList,
            ref _assignValsWorkList);
        Scribe_Collections.Look(ref _doneRoomList, "doneRoomIDs", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _roomAssignments ??= [];
            _doneRoomList    ??= [];
            _doneRoomIDs.Clear();
            foreach (int id in _doneRoomList)
                _doneRoomIDs.Add(id);
        }
        else if (Scribe.mode == LoadSaveMode.Saving)
        {
            _doneRoomList = [.._doneRoomIDs];
        }
    }
}
