using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Kidnapping sequence — the "safe to move" gate is handled in LordToil_RHF_Assault
/// before this job is ever issued, so the driver just executes:
///   1. Walk to the downed target.
///   2. Pick them up physically via Toils_Haul.StartCarryThing.
///   3. Path to the nearest map edge.
///   4. ExitMap — Pawn.cs auto-calls faction.kidnapped.Kidnap() when the
///      carrier's faction differs from the carried pawn's faction.
/// </summary>
public class JobDriver_RHF_KidnapAndFlee : JobDriver
{
    private Pawn Victim => (Pawn)job.GetTarget(TargetIndex.A).Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => Victim == null || Victim.Dead);

        this.AddFinishAction(_ =>
        {
            (pawn.GetLord()?.LordJob as LordJob_RHF_KidnappingRaid)?.OnKidnapComplete(pawn);
        });

        // --- 1. Walk to downed victim ---
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
            .FailOn(() => !Victim.Downed);

        // --- 2. Pick up victim (vanilla carry system) ---
        // job.count = 1 is set by JobGiver_RHF_KidnapDowned.
        yield return Toils_Haul.StartCarryThing(TargetIndex.A);

        // --- 3. Path to nearest map edge ---
        Toil gotoEdge = ToilMaker.MakeToil();
        gotoEdge.initAction = () =>
        {
            if (RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 exitSpot, TraverseMode.ByPawn))
                pawn.pather.StartPath(exitSpot, PathEndMode.OnCell);
            else
                EndJobWith(JobCondition.Incompletable);
        };
        gotoEdge.defaultCompleteMode = ToilCompleteMode.PatherArrival;
        yield return gotoEdge;

        // --- 4. Exit map ---
        // Pawn.ExitMap detects faction mismatch → auto-calls faction.kidnapped.Kidnap().
        Toil exitMap = ToilMaker.MakeToil();
        exitMap.initAction = () => pawn.ExitMap(true, ComputeExitDirection(pawn.Position, pawn.Map));
        exitMap.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return exitMap;
    }

    private static Rot4 ComputeExitDirection(IntVec3 pos, Map map)
    {
        int toWest  = pos.x;
        int toEast  = map.Size.x - 1 - pos.x;
        int toSouth = pos.z;
        int toNorth = map.Size.z - 1 - pos.z;

        if (toWest  <= toEast  && toWest  <= toSouth && toWest  <= toNorth) return Rot4.West;
        if (toEast  <= toSouth && toEast  <= toNorth)                        return Rot4.East;
        return toSouth <= toNorth ? Rot4.South : Rot4.North;
    }
}
