using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ReptileHunterFaction;

public class SymbolResolver_PH_Butchery : SymbolResolver
{
    public override void Resolve(ResolveParams rp)
    {
        Map map = BaseGen.globalSettings.map;
        if (map == null) return;

        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

        // Find all coolers placed within the settlement rect
        var coolers = map.listerThings.GetThingsOfType<Building>()
            .Where(b => b.def == ThingDefOf.Cooler && rp.rect.Contains(b.Position))
            .ToList();

        if (coolers.Count == 0) return;

        // Set all coolers to -9°C (meat storage temperature)
        foreach (var cooler in coolers)
        {
            var tempControl = cooler.GetComp<CompTempControl>();
            if (tempControl != null)
                tempControl.targetTemperature = -9f;
        }

        // Collect all distinct rooms on the cold side of each cooler.
        // A cooler is a wall building; cold air blows toward IntVec3.South rotated
        // by the cooler's facing — the same cell the vanilla Building_Cooler cools.
        var coolerRooms = coolers
            .Select(c => (c.Position + IntVec3.South.RotatedBy(c.Rotation)).GetRoom(map))
            .Where(r => r != null)
            .Distinct()
            .ToList();

        if (coolerRooms.Count == 0) return;

        Faction? captorFaction = rp.faction ?? map.ParentFaction;
        bool isLastSettlement = captorFaction != null
            && Find.WorldObjects.Settlements.Count(s => s.Faction == captorFaction) <= 1;

        var spoils = WorldComp_SpoilsOfBattle.Get();
        var toRemove = new List<Corpse>();

        foreach (Room room in coolerRooms)
        {
            var cells = room!.Cells.Where(c => c.Walkable(map)).ToList();
            if (cells.Count == 0) continue;

            PlaceWorldCompCorpsesInRoom(map, cells, isLastSettlement, spoils, toRemove);
            SpawnGeneratedCorpsesInRoom(map, cells, captorFaction);
        }

        // Remove all placed WorldComp corpses after iterating to avoid modifying the list mid-loop
        foreach (var c in toRemove)
            spoils?.corpses.Remove(c);
    }

    /// <summary>
    /// Places WorldComp_SpoilsOfBattle corpses into a single cooler room.
    /// All are placed if this is the faction's last settlement; otherwise each has a 20% chance.
    /// Corpses to remove are collected into <paramref name="toRemove"/> for deferred removal.
    /// </summary>
    private static void PlaceWorldCompCorpsesInRoom(
        Map map, List<IntVec3> cells, bool isLastSettlement,
        WorldComp_SpoilsOfBattle? spoils, List<Corpse> toRemove)
    {
        if (spoils == null || spoils.corpses.Count == 0) return;

        foreach (Corpse corpse in spoils.corpses)
        {
            if (corpse.DestroyedOrNull()) { toRemove.Add(corpse); continue; }
            if (!isLastSettlement && !Rand.Chance(0.2f)) continue;

            if (corpse.Spawned)
                corpse.DeSpawn();

            GenPlace.TryPlaceThing(corpse, cells.RandomElement(), map, ThingPlaceMode.Near);
            toRemove.Add(corpse);
        }
    }

    /// <summary>
    /// Generates 2–5 fresh humanlike corpses with bite wounds and places them in one cooler room.
    /// </summary>
    private static void SpawnGeneratedCorpsesInRoom(Map map, List<IntVec3> cells, Faction? captorFaction)
    {
        int count = Rand.Range(2, 6); // 2–5 inclusive
        for (int i = 0; i < count; i++)
        {
            Corpse? corpse = GenerateDeadPawnWithBites(map, captorFaction);
            if (corpse == null) continue;
            GenPlace.TryPlaceThing(corpse, cells.RandomElement(), map, ThingPlaceMode.Near);
        }
    }

    private static Corpse? GenerateDeadPawnWithBites(Map map, Faction? captorFaction)
    {
        Faction? victimFaction = PickVictimFaction(captorFaction);
        Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            kind: PawnKindDefOf.Colonist,
            faction: victimFaction,
            context: PawnGenerationContext.NonPlayer,
            tile: map.Tile));

        ApplyBiteWounds(pawn);

        pawn.health.SetDead();
        Corpse corpse = pawn.MakeCorpse(null, null);
        Find.WorldPawns.PassToWorld(pawn);
        return corpse;
    }

    private static Faction? PickVictimFaction(Faction? captorFaction)
    {
        var candidates = Find.FactionManager.AllFactions
            .Where(f => !f.IsPlayer && f != captorFaction && !f.def.hidden && !f.defeated)
            .ToList();
        return candidates.Count > 0 ? candidates.RandomElement() : null;
    }

    /// <summary>
    /// Adds 2–4 bite wound hediffs to random outside body parts, simulating predator kills.
    /// Uses permanent scarring to avoid any live-pawn bleeding or kill triggers.
    /// </summary>
    private static void ApplyBiteWounds(Pawn pawn)
    {
        var parts = pawn.health.hediffSet
            .GetNotMissingParts(depth: BodyPartDepth.Outside)
            .ToList();
        if (parts.Count == 0) return;

        int woundCount = Rand.Range(2, 5); // 2–4 inclusive
        foreach (BodyPartRecord part in parts.InRandomOrder().Take(woundCount))
        {
            var injury = (Hediff_Injury)HediffMaker.MakeHediff(DamageDefOf.Bite.hediff, pawn, part);
            injury.Severity = part.def.GetMaxHealth(pawn) * Rand.Range(0.3f, 0.9f);
            if (injury.TryGetComp<HediffComp_GetsPermanent>() is { } perm)
                perm.IsPermanent = true;
            pawn.health.AddHediff(injury, part);
        }
    }
}
