using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ReptileHunterFaction;

/// <summary>
/// Post-processes a PH_HuntingHut structure used as a kidnapped-pawn prison site.
///
/// Handles four responsibilities that require runtime data unavailable to KCSG layouts:
///   1. Prisoner — retrieves the kidnapped pawn from the site, spawns it on the SleepingSpot,
///      sets prisoner status, applies leg/bite damage.
///   2. Meals — fills the shelf in the guard room with survival meals (human-meat ingredient),
///      raw human meat, and optional drug medicine / SBD stash.
///   3. Guards — spawns 0–2 RHF fighters near the bedroom beds.
///   4. Mines — scatters IED / spike traps in a ring around the structure.
/// </summary>
public class SymbolResolver_RHF_HuntingHutPrison : SymbolResolver
{
    private const string PrisonSitePartDefName = "RHF_KidnappedPawnPrison";

    private const int MineSpawnMinCount = 15;
    private const int MineSpawnMaxCount = 20;
    private const int MineSpawnPadding = 9;
    private const int MineMinSpacing = 5;

    public override void Resolve(ResolveParams rp)
    {
        Map map = BaseGen.globalSettings.map;
        Log.Message($"Resolving {GetType().Name} for map {map?.Tile}");
        if (map == null) return;

        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

        Pawn? kidnappedPawn = TryGetKidnappedPawnForMap(map);
        Faction captorFaction = rp.faction ?? map.ParentFaction;

        PlacePrisoner(map, rp.rect, kidnappedPawn, captorFaction);
        FillGuardShelf(map, rp.rect, captorFaction);
        SpawnGuards(map, rp.rect, captorFaction);
        SpawnMinesAroundRect(map, rp.rect, captorFaction);
        TryDestroyScanner(map, rp.rect);
    }

    // ── Scanner ───────────────────────────────────────────────────────────────

    private static void TryDestroyScanner(Map map, CellRect rect)
    {
        if (!Rand.Chance(0.5f)) return;

        ThingDef? scannerDef = DefDatabase<ThingDef>.GetNamedSilentFail("LongRangeAncientComplexScanner");
        if (scannerDef == null) return;

        map.listerThings.ThingsOfDef(scannerDef)
            .FirstOrDefault(t => rect.Contains(t.Position))
            ?.Destroy();
    }

    // ── Prisoner ─────────────────────────────────────────────────────────────

    private static Pawn? TryGetKidnappedPawnForMap(Map map)
    {
        if (map.Parent is not Site site || site.parts == null)
            return null;

        return TryTakePawnFromParts(site.parts, PrisonSitePartDefName)
               ?? TryTakePawnFromParts(site.parts, null);
    }

    private static Pawn? TryTakePawnFromParts(List<SitePart> parts, string? requiredDef)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            SitePart part = parts[i];
            if (requiredDef != null && part?.def?.defName != requiredDef) continue;
            if (part?.things == null || !part.things.Any) continue;

            for (int j = 0; j < part.things.Count; j++)
            {
                if (part.things[j] is Pawn pawn)
                {
                    part.things.Remove(pawn);
                    return pawn;
                }
            }
        }
        return null;
    }

    private static void PlacePrisoner(Map map, CellRect rect, Pawn? pawn, Faction captorFaction)
    {
        Building_Bed? sleepingSpot = map.listerThings.GetThingsOfType<Building_Bed>()
            .FirstOrDefault(b => b.def == ThingDefOf.SleepingSpot && rect.Contains(b.Position));

        if (sleepingSpot == null) return;

        sleepingSpot.ForPrisoners = true;

        if (pawn == null) return;

        // Remove kidnapper's record and any lost-pawn thoughts
        captorFaction?.kidnapped?.RemoveKidnappedPawn(pawn);
        PawnDiedOrDownedThoughtsUtility.RemoveLostThoughts(pawn);
        pawn.SetFaction(null);

        // Crush legs — same as GenStep_RHFPrison
        foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
        {
            if (part.def.defName.Contains("Leg"))
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Crush, 70f, hitPart: part));
        }

        ApplyBiteDamage(pawn);

        if (pawn.SpawnedOrAnyParentSpawned)
            pawn.DeSpawnOrDeselect();

        GenSpawn.Spawn(pawn, sleepingSpot.Position, map);
        pawn.guest.SetGuestStatus(captorFaction, GuestStatus.Prisoner);
        pawn.mindState.willJoinColonyIfRescuedInt = true;
        sleepingSpot.CompAssignableToPawn?.TryAssignPawn(pawn);

        // Scatter human meat near the sleeping spot
        for (int i = 0; i < 3; i++)
        {
            IntVec3 meatPos = sleepingSpot.Position + new IntVec3(Rand.Range(-1, 2), 0, Rand.Range(-1, 2));
            if (meatPos.InBounds(map))
                GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDefOf.Meat_Human), meatPos, map, ThingPlaceMode.Near);
        }
    }

    // ── Guard shelf ───────────────────────────────────────────────────────────

    private static void FillGuardShelf(Map map, CellRect rect, Faction captorFaction)
    {
        var shelves = map.listerThings.GetThingsOfType<Building_Storage>()
            .Where(s => rect.Contains(s.Position))
            .ToList();

        if (shelves.Count == 0) return;

        // Clear anything KCSG may have placed on the shelf cells
        foreach (var shelf in shelves)
        {
            foreach (IntVec3 cell in shelf.OccupiedRect().Cells)
            {
                foreach (Thing item in cell.GetThingList(map).Where(t => t is not Building).ToList())
                    item.Destroy();
            }
        }

        // Collect all shelf cells across all shelves in the rect
        var cells = shelves.SelectMany(s => s.OccupiedRect().Cells).ToList();

        // Fill cells with survival meals (human-meat ingredient) — up to 3 stacks per cell
        foreach (IntVec3 cell in cells)
        {
            int stacks = Rand.RangeInclusive(1, 2);
            for (int i = 0; i < stacks; i++)
            {
                Log.Message($"Spawning meal on shelf at {cell}");
                Thing meal = ThingMaker.MakeThing(ThingDefOf.MealSurvivalPack);
                meal.TryGetComp<CompIngredients>()?.RegisterIngredient(ThingDefOf.Meat_Human);
                GenSpawn.Spawn(meal, cell, map);
            }
        }

        // 30% chance: drug medicine stash on shelf
        if (Rand.Chance(0.30f))
        {
            Thing drugMed = ThingMaker.MakeThing(ReptileHunterFactionDefOf.RHF_DrugMedicine);
            drugMed.stackCount = Rand.RangeInclusive(1, 3);
            GenSpawn.Spawn(drugMed, cells.RandomElement(), map);
        }

        // 5% chance: single SBD on shelf
        if (Rand.Chance(0.05f))
        {
            Thing sbd = ThingMaker.MakeThing(ReptileHunterFactionDefOf.RHF_SBD);
            GenSpawn.Spawn(sbd, cells.RandomElement(), map);
        }
    }

    // ── Guards ────────────────────────────────────────────────────────────────

    private static void SpawnGuards(Map map, CellRect rect, Faction captorFaction)
    {
        // 30% = 0 guards, 60% = 1 guard, 10% = 2 guards
        float roll = Rand.Value;
        int count = roll < 0.30f ? 0 : (roll < 0.90f ? 1 : 2);
        if (count == 0) return;

        Faction? hunterFaction = Find.FactionManager.FirstFactionOfDef(ReptileHunterFactionDefOf.RHF_ReptileHunters);
        if (hunterFaction == null) return;

        // Prefer walkable cells near beds in the bedroom (the larger upper room)
        var beds = map.listerThings.GetThingsOfType<Building_Bed>()
            .Where(b => b.def != ThingDefOf.SleepingSpot && rect.Contains(b.Position))
            .ToList();

        List<IntVec3> spawnCells;
        if (beds.Count > 0)
        {
            spawnCells = beds
                .SelectMany(b => GenAdj.CellsAdjacent8Way(b))
                .Where(c => c.InBounds(map) && c.Walkable(map) && c.GetFirstPawn(map) == null && rect.Contains(c))
                .Distinct()
                .ToList();
        }
        else
        {
            // Fallback: any walkable cell in the rect
            spawnCells = rect.Cells
                .Where(c => c.InBounds(map) && c.Walkable(map) && c.GetFirstPawn(map) == null)
                .ToList();
        }

        if (spawnCells.Count == 0) return;

        for (int i = 0; i < count; i++)
        {
            if (spawnCells.Count == 0) break;

            IntVec3 pos = spawnCells.RandomElement();
            spawnCells.Remove(pos);

            PawnKindDef kind = Rand.Bool
                ? ReptileHunterFactionDefOf.RHF_ReptileHuntersFighter_Ranged
                : ReptileHunterFactionDefOf.RHF_ReptileHuntersFighter_Melee;

            Pawn guard = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind: kind,
                faction: hunterFaction,
                tile: map.Tile));

            GenSpawn.Spawn(guard, pos, map);
        }
    }

    // ── Mines ─────────────────────────────────────────────────────────────────

    private static void SpawnMinesAroundRect(Map map, CellRect rect, Faction captorFaction)
    {
        ThingDef mineDef = DefDatabase<ThingDef>.GetNamedSilentFail("TrapIED_HighExplosive") ?? ThingDefOf.TrapSpike;

        int minX = rect.minX - MineSpawnPadding;
        int maxX = rect.maxX + MineSpawnPadding;
        int minZ = rect.minZ - MineSpawnPadding;
        int maxZ = rect.maxZ + MineSpawnPadding;

        int target = Rand.RangeInclusive(MineSpawnMinCount, MineSpawnMaxCount);
        int placed = 0;
        int attempts = 0;
        int maxAttempts = target * 20;
        List<IntVec3> placed_cells = [];

        while (placed < target && attempts < maxAttempts)
        {
            attempts++;
            IntVec3 cell = new(Rand.RangeInclusive(minX, maxX), map.Center.y, Rand.RangeInclusive(minZ, maxZ));
            if (!cell.InBounds(map) || rect.Contains(cell) || !cell.Standable(map) || cell.GetEdifice(map) != null)
                continue;
            if (cell.GetThingList(map).Count > 0)
                continue;
            if (placed_cells.Any(c => c.DistanceTo(cell) < MineMinSpacing))
                continue;

            Thing mine = ThingMaker.MakeThing(mineDef, mineDef.MadeFromStuff ? GenStuff.DefaultStuffFor(mineDef) : null);
            GenSpawn.Spawn(mine, cell, map);
            placed_cells.Add(cell);
            placed++;
        }
    }

    // ── Bite damage ───────────────────────────────────────────────────────────

    private static void ApplyBiteDamage(Pawn pawn)
    {
        TryBitePart(pawn, "Arm", BodyPartDepth.Outside);
        TryBitePart(pawn, "Leg", BodyPartDepth.Outside);
        TryBitePart(pawn, "Kidney", BodyPartDepth.Inside);
        TryBitePart(pawn, "Lung", BodyPartDepth.Inside);
    }

    private static void TryBitePart(Pawn pawn, string partName, BodyPartDepth depth)
    {
        if (pawn.Dead || !Rand.Chance(0.1f)) return;

        List<BodyPartRecord> matching = [];
        foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts(depth: depth))
        {
            if (part.def.defName.Contains(partName))
                matching.Add(part);
        }

        if (matching.Count == 0) return;

        BodyPartRecord target = matching.RandomElement();
        float damage = target.def.GetMaxHealth(pawn) * 1.2f;
        pawn.TakeDamage(new DamageInfo(DamageDefOf.Bite, damage, 0f, -1f, null, target));
    }
}
