using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Carry a player pawn's corpse off the map and store it in WorldComp_SpoilsOfBattle.
///
/// Step sequence:
///   1. Walk to the corpse.
///   2. Pick it up via Toils_Haul.StartCarryThing.
///   3. Path to the nearest map edge.
///   4. Drop + DeSpawn the corpse (so ExitMap won't Destroy it), store it in WorldComp, then exit.
/// </summary>
public class JobDriver_RHF_CarryCorpseOffMap : JobDriver
{
    private Corpse Corpse => (Corpse)job.GetTarget(TargetIndex.A).Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => pawn.Reserve(Corpse, job, 1, -1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.AddFinishAction(_ =>
        {
            (pawn.GetLord()?.LordJob as LordJob_RHF_KidnappingRaidBig)
                ?.OnCorpseCarryComplete(pawn);
        });

        // --- 1. Walk to corpse ---
        // FailOn only valid before pickup — after StartCarryThing the corpse is no longer world-spawned.
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
            .FailOn(() => Corpse == null || !Corpse.Spawned);

        // --- 2. Pick up corpse ---
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

        // --- 4. Extract corpse from carry tracker, then exit map ---
        Toil exitToil = ToilMaker.MakeToil();
        exitToil.initAction = () =>
        {
            // Drop the corpse onto the ground, then despawn it so ExitMap won't destroy it.
            // We can then store the live object in WorldComp rather than just the name.
            if (pawn.carryTracker.CarriedThing is Corpse carried)
            {
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out _);
                if (carried.Spawned) carried.DeSpawn();
                WorldComp_SpoilsOfBattle.Get()?.AddCorpse(carried);
            }

            pawn.ExitMap(true, ComputeExitDirection(pawn.Position, pawn.Map));
        };
        exitToil.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return exitToil;
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
