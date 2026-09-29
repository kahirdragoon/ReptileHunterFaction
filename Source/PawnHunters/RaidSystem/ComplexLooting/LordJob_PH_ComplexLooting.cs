using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnHunters;

public class LordJob_PH_ComplexLooting : LordJob
{
    // ── Room assignment ──────────────────────────────────────────────────────
    // Rooms are keyed by their lowest cell index. Room.ID is a runtime counter and rooms are rebuilt on load,
    // so saved IDs would point at nothing or at the wrong room; the cell key is stable while the walls are.

    // raider → room key they are currently exploring (serialized)
    private Dictionary<Pawn, int> _roomAssignments    = [];
    private List<Pawn>            _assignKeysWorkList  = [];
    private List<int>             _assignValsWorkList  = [];

    // room keys that are fully explored (no crate / crate looted / threat-skipped)
    private HashSet<int> _doneRoomKeys = [];
    private List<int>    _doneRoomList = [];

    // Room.ID → room key. Not saved: a rebuilt room gets a new ID and its key is computed again.
    private readonly Dictionary<int, int> _roomKeyCache = [];

    // ── LordJob overrides ────────────────────────────────────────────────────

    public override bool GuiltyOnDowned => false;

    public override StateGraph CreateGraph()
    {
        var toil_loot    = new LordToil_PH_ComplexLoot();
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

    private int RoomKey(Room room)
    {
        if (!_roomKeyCache.TryGetValue(room.ID, out int key))
        {
            key = int.MaxValue;
            CellIndices indices = room.Map.cellIndices;
            foreach (IntVec3 cell in room.Cells)
                key = Math.Min(key, indices.CellToIndex(cell));
            _roomKeyCache[room.ID] = key;
        }
        return key;
    }

    public bool TryAssignRoom(Pawn pawn, Room room)
    {
        int key = RoomKey(room);
        if (_roomAssignments.ContainsKey(pawn))   return false;
        if (_roomAssignments.ContainsValue(key))  return false;
        if (_doneRoomKeys.Contains(key))          return false;
        _roomAssignments[pawn] = key;
        return true;
    }

    /// <summary>Release the room without marking it done — another raider may try it.</summary>
    public void UnassignRoom(Pawn pawn) => _roomAssignments.Remove(pawn);

    /// <summary>Release the room AND mark it done — no raider will revisit.</summary>
    public void FinishRoom(Pawn pawn)
    {
        if (_roomAssignments.TryGetValue(pawn, out int key))
            _doneRoomKeys.Add(key);
        _roomAssignments.Remove(pawn);
    }

    public bool HasRoomAssignment(Pawn pawn) => _roomAssignments.ContainsKey(pawn);
    public bool IsRoomAssigned(Room room)    => _roomAssignments.ContainsValue(RoomKey(room));
    public bool IsRoomDone(Room room)        => _doneRoomKeys.Contains(RoomKey(room));

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
        // New labels on purpose: older saves stored runtime Room.IDs under "roomAssignments"/"doneRoomIDs",
        // which are meaningless now and are dropped on load.
        Scribe_Collections.Look(
            ref _roomAssignments,
            "roomAssignmentKeys",
            LookMode.Reference,
            LookMode.Value,
            ref _assignKeysWorkList,
            ref _assignValsWorkList);
        Scribe_Collections.Look(ref _doneRoomList, "doneRoomKeys", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _roomAssignments ??= [];
            _doneRoomList    ??= [];
            _doneRoomKeys.Clear();
            foreach (int key in _doneRoomList)
                _doneRoomKeys.Add(key);
        }
        else if (Scribe.mode == LoadSaveMode.Saving)
        {
            _doneRoomList = [.._doneRoomKeys];
        }
    }
}
