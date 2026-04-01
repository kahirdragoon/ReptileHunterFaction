using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Explores a single room:
///   1. Walk to the assigned room.
///   2. Wander inside for ~10 seconds (250 ticks) — mimics searching.
///   3. Check whether the room contains a crate with contents.
///      — No crate: mark room done, job succeeds (raider picks next room).
///      — Crate found: path to it, work for 60 ticks, open and loot.
///   4. Mark room done.
///
/// FinishAction always releases the room assignment:
///   Succeeded  → FinishRoom (mark done, never revisited)
///   Otherwise  → UnassignRoom (another raider may retry)
/// </summary>
public class JobDriver_RHF_ExploreRoom : JobDriver
{
    private IntVec3        TargetCell  => job.GetTarget(TargetIndex.A).Cell;
    private Building_Crate TargetCrate => (Building_Crate)job.GetTarget(TargetIndex.B).Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.AddFinishAction(condition =>
        {
            var lordJob = pawn.GetLord()?.LordJob as LordJob_RHF_ComplexLooting;
            if (lordJob == null) return;
            if (condition == JobCondition.Succeeded)
                lordJob.FinishRoom(pawn);
            else
                lordJob.UnassignRoom(pawn);
        });

        // --- 1. Walk to the assigned room ---
        yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

        // --- 2. Wander inside the room for ~10 seconds (250 ticks) ---
        Toil wander = ToilMaker.MakeToil();
        int finishAt = -1;
        wander.initAction = () => finishAt = Find.TickManager.TicksGame + 250;
        wander.tickAction = () =>
        {
            if (finishAt >= 0 && Find.TickManager.TicksGame >= finishAt)
            {
                ReadyForNextToil();
                return;
            }
            // Every ~45 ticks, move to a random cell within the current room
            if (!pawn.pather.Moving && pawn.IsHashIntervalTick(45))
            {
                Room? room = pawn.Position.GetRoom(pawn.Map);
                if (room != null && !room.UsesOutdoorTemperature)
                {
                    var dest = new List<IntVec3>();
                    foreach (IntVec3 cell in room.Cells)
                    {
                        if (cell != pawn.Position && cell.Standable(pawn.Map))
                            dest.Add(cell);
                    }
                    if (dest.Count > 0)
                        pawn.pather.StartPath(dest.RandomElement(), PathEndMode.OnCell);
                }
            }
        };
        wander.defaultCompleteMode = ToilCompleteMode.Never;
        yield return wander;

        // --- 3. Check whether the room has a crate ---
        Toil checkCrate = ToilMaker.MakeToil();
        checkCrate.initAction = () =>
        {
            Building_Crate? crate = FindCrateInRoom(TargetCell.GetRoom(pawn.Map), pawn.Map);

            if (crate == null)
            {
                // No crate — room exploration is complete
                EndJobWith(JobCondition.Succeeded);
            }
            else
            {
                job.SetTarget(TargetIndex.B, crate);
            }
        };
        checkCrate.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return checkCrate;

        // --- 4. Walk to the crate ---
        yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch)
            .FailOnDespawnedOrNull(TargetIndex.B);

        // --- 5. Face the crate, then work time (opening the crate) ---
        Toil faceCrate = ToilMaker.MakeToil();
        faceCrate.initAction = () =>
        {
            Building_Crate c = TargetCrate;
            if (c != null) pawn.rotationTracker.FaceTarget(c);
        };
        faceCrate.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return faceCrate;
        yield return Toils_General.Wait(60)
            .FailOnDespawnedOrNull(TargetIndex.B)
            .WithProgressBarToilDelay(TargetIndex.B);

        // --- 6. Open crate and collect loot ---
        Toil openAndLoot = ToilMaker.MakeToil();
        openAndLoot.initAction = () =>
        {
            Building_Crate crate = TargetCrate;
            if (crate == null || !crate.Spawned || !crate.HasAnyContents) return;

            var lootItems = new List<Thing>();
            foreach (Thing t in crate.GetDirectlyHeldThings())
            {
                if (IsValuableLoot(t)) lootItems.Add(t);
            }

            crate.Open();

            foreach (Thing item in lootItems)
            {
                if (item.Spawned)
                    pawn.inventory.TryAddItemNotForSale(item);
            }
        };
        openAndLoot.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return openAndLoot;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Building_Crate? FindCrateInRoom(Room? room, Map map)
    {
        if (room == null) return null;
        foreach (IntVec3 cell in room.Cells)
        {
            foreach (Thing t in map.thingGrid.ThingsListAt(cell))
            {
                if (t is Building_Crate c && c.HasAnyContents)
                    return c;
            }
        }
        return null;
    }

    public static bool IsValuableLoot(Thing item)
    {
        if (item.def.IsApparel || item.def.IsWeapon) return false;
        if (item.def == ThingDefOf.Luciferium)        return true;
        if (item.def == ThingDefOf.MedicineUltratech) return true;
        if (item.def == ThingDefOf.ComponentSpacer)   return true;
        return item.MarketValue * item.stackCount >= 50f && item.def.BaseMarketValue >= 5f;
    }
}
