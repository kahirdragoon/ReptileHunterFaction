using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ReptileHunterFaction;

public class SymbolResolver_PH_Prisoners : SymbolResolver
{
    // Maximum cell distance from a sleeping spot group to an extractor for them
    // to be considered part of the same structure (covers the largest layout, ~13x12).
    private const int SameStructureRadius = 10;

    public override void Resolve(ResolveParams rp)
    {
        Map map = BaseGen.globalSettings.map;
        if (map == null) return;

        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

        var allSpots = map.listerThings.GetThingsOfType<Building_Bed>()
            .Where(b => b.def == ThingDefOf.SleepingSpot && rp.rect.Contains(b.Position))
            .ToList();

        if (allSpots.Count == 0) return;

        // Mark every sleeping spot in the settlement as a prisoner bed
        foreach (var spot in allSpots)
            spot.ForPrisoners = true;

        // Cache extractor positions once for the proximity checks below
        var extractorPositions = map.listerThings.GetThingsOfType<Building>()
            .Where(b => b.def == ReptileHunterFactionDefOf.RHF_SBD_Extractor && rp.rect.Contains(b.Position))
            .Select(b => b.Position)
            .ToList();

        Faction captorFaction = rp.faction ?? map.ParentFaction;

        // Process each room independently so meat and prisoners stay local
        foreach (var group in allSpots.GroupBy(b => b.GetRoom()))
        {
            var spots = group.ToList();
            Room? room = group.Key;

            SpawnMeatChunks(map, room, spots);

            // An extractor is in the "same building" when it falls within the
            // bounding rect of this room's sleeping spots expanded by the max
            // structure size — avoids needing KCSG structure metadata at runtime.
            CellRect spotBounds = CellRect.FromLimits(
                spots.Min(s => s.Position.x),
                spots.Min(s => s.Position.z),
                spots.Max(s => s.Position.x),
                spots.Max(s => s.Position.z)).ExpandedBy(SameStructureRadius);

            bool hasNearbyExtractor = extractorPositions.Any(p => spotBounds.Contains(p));

            int pawnCount = Math.Max(1, (int)Math.Round(spots.Count * Rand.Range(0.3f, 0.6f)));

            foreach (var spot in spots.InRandomOrder().Take(pawnCount))
            {
                Pawn prisoner = GeneratePrisoner(map, hasNearbyExtractor, captorFaction);
                if (prisoner == null) continue;

                ApplyRandomBiteDamage(prisoner);
                if (prisoner.Dead) continue;

                if (prisoner.SpawnedOrAnyParentSpawned)
                    prisoner.DeSpawnOrDeselect();

                GenSpawn.Spawn(prisoner, spot.Position, map);
                prisoner.guest.SetGuestStatus(captorFaction, GuestStatus.Prisoner);
                prisoner.mindState.willJoinColonyIfRescuedInt = true;
                spot.CompAssignableToPawn?.TryAssignPawn(prisoner);
            }
        }
    }

    // Spawn meat only within the specific room containing these sleeping spots.
    private static void SpawnMeatChunks(Map map, Room? room, List<Building_Bed> spots)
    {
        List<IntVec3> cells;
        if (room != null)
        {
            cells = room.Cells.Where(c => c.Walkable(map)).ToList();
        }
        else
        {
            // Fallback: walkable neighbours of the spots themselves
            cells = spots
                .SelectMany(s => GenAdj.CellsAdjacent8Way(s))
                .Where(c => c.InBounds(map) && c.Walkable(map))
                .Distinct()
                .ToList();
        }

        if (cells.Count == 0) return;

        int count = Math.Max(1, (int)Math.Round(cells.Count / 6.0 * Rand.Range(0.5f, 1.5f)));
        for (int i = 0; i < count; i++)
        {
            ThingDef meatDef = Rand.Chance(0.5f) ? ThingDefOf.Meat_Human : VanillaDefOf.Meat_Megaspider;
            Thing meat = ThingMaker.MakeThing(meatDef);
            meat.stackCount = Rand.Range(2, 6);
            GenPlace.TryPlaceThing(meat, cells.RandomElement(), map, ThingPlaceMode.Near);
        }
    }

    private static Faction? PickPrisonerFaction(Faction? captorFaction)
    {
        var candidates = Find.FactionManager.AllFactions
            .Where(f => !f.IsPlayer && f != captorFaction && !f.def.hidden && !f.defeated)
            .ToList();
        return candidates.Count > 0 ? candidates.RandomElement() : null;
    }

    private static Pawn GeneratePrisoner(Map map, bool geneMatchRequired, Faction captorFaction)
    {
        // Try to place an actual kidnapped player pawn in this cell.
        // Always use one if this is the last RHF settlement; otherwise 5% chance.
        if (captorFaction?.kidnapped != null)
        {
            bool isLastSettlement = Find.WorldObjects.Settlements
                .Count(s => s.Faction == captorFaction) <= 1;

            if (isLastSettlement || Rand.Chance(0.05f))
            {
                var kidnappedList = captorFaction.kidnapped.KidnappedPawnsListForReading;
                if (kidnappedList?.Count > 0)
                {
                    Pawn? kidnapped = kidnappedList
                        .Where(p => p != null && !p.DestroyedOrNull() && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer)
                        .RandomElementWithFallback();

                    if (kidnapped != null)
                    {
                        captorFaction.kidnapped.RemoveKidnappedPawn(kidnapped);
                        PawnDiedOrDownedThoughtsUtility.RemoveLostThoughts(kidnapped);
                        kidnapped.SetFaction(null);

                        if (geneMatchRequired && ModLister.BiotechInstalled)
                        {
                            var settings = ReptileHunterFactionMod.Settings;
                            TryApplyTargetGenes(kidnapped, settings);
                        }

                        return kidnapped;
                    }
                }
            }
        }

        Faction? prisonerFaction = PickPrisonerFaction(captorFaction);

        if (geneMatchRequired && ModLister.BiotechInstalled)
        {
            var settings = ReptileHunterFactionMod.Settings;
            if (settings.targetXenotypes?.Count > 0 || settings.targetGenes?.Count > 0)
                return GenerateGeneMatchingPrisoner(map, settings, prisonerFaction);
        }

        return GenerateBasePrisoner(map, prisonerFaction);
    }

    private static Pawn GenerateGeneMatchingPrisoner(Map map, RHFModSettings settings, Faction? prisonerFaction)
    {
        Pawn pawn = GenerateBasePrisoner(map, prisonerFaction);
        TryApplyTargetGenes(pawn, settings);
        return pawn;
    }

    // Overwrites a pawn's exogenes to match configured xenotypes/genes, mirroring xenogerm implantation.
    // Endogenes are preserved. No-ops if the pawn has no gene tracker or no targets are configured.
    private static void TryApplyTargetGenes(Pawn pawn, RHFModSettings settings)
    {
        if (pawn.genes == null) return;

        if (settings.targetXenotypes?.Count > 0)
        {
            XenotypeDef? xenotype = settings.targetXenotypes
                .Select(n => DefDatabase<XenotypeDef>.GetNamedSilentFail(n))
                .Where(x => x != null)
                .RandomElementWithFallback();

            if (xenotype != null)
            {
                GeneUtility.UpdateXenogermReplication(pawn);
                pawn.genes.SetXenotype(XenotypeDefOf.Baseliner);
                pawn.genes.xenotypeName = xenotype.label;
                foreach (var geneDef in xenotype.genes)
                    pawn.genes.AddGene(geneDef, true);
                return;
            }
        }

        if (settings.targetGenes?.Count > 0)
        {
            var genes = settings.targetGenes
                .Select(n => DefDatabase<GeneDef>.GetNamedSilentFail(n))
                .Where(g => g != null)
                .ToList();

            if (genes.Count > 0)
            {
                GeneUtility.UpdateXenogermReplication(pawn);
                pawn.genes.SetXenotype(XenotypeDefOf.Baseliner);
                pawn.genes.xenotypeName = "RHF_SelectedGenes".Translate();
                foreach (var geneDef in genes)
                    pawn.genes.AddGene(geneDef!, true);
            }
        }
    }

    private static Pawn GenerateBasePrisoner(Map map, Faction? prisonerFaction)
    {
        return PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            kind: PawnKindDefOf.Colonist,
            faction: prisonerFaction,
            context: PawnGenerationContext.NonPlayer,
            tile: map.Tile));
    }

    private static void ApplyRandomBiteDamage(Pawn pawn)
    {
        if (Rand.Chance(0.2f))
        {
            var part = GetRandomBodyPartOfType(pawn, "Arm", BodyPartDepth.Outside);
            if (part != null) ApplyBiteToLimb(pawn, part);
        }
        if (!pawn.Dead && Rand.Chance(0.2f))
        {
            var part = GetRandomBodyPartOfType(pawn, "Leg", BodyPartDepth.Outside);
            if (part != null) ApplyBiteToLimb(pawn, part);
        }
        if (!pawn.Dead && Rand.Chance(0.2f))
        {
            var part = GetRandomBodyPartOfType(pawn, "Torso", BodyPartDepth.Inside);
            if (part != null) ApplyBiteToLimb(pawn, part);
        }
    }

    private static BodyPartRecord? GetRandomBodyPartOfType(Pawn pawn, string partName, BodyPartDepth depth)
    {
        List<BodyPartRecord> matching = [];
        foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts(depth: depth))
        {
            if (part.def.defName.Contains(partName))
                matching.Add(part);
        }
        return matching.Count > 0 ? matching.RandomElement() : null;
    }

    private static void ApplyBiteToLimb(Pawn pawn, BodyPartRecord part)
    {
        if (pawn.Dead) return;

        if (Rand.Chance(0.5f))
        {
            // Healed scar — permanent bite injury, no bleeding
            var injury = (Hediff_Injury)HediffMaker.MakeHediff(DamageDefOf.Bite.hediff, pawn, part);
            injury.Severity = part.def.GetMaxHealth(pawn) * Rand.Range(0.2f, 0.6f);
            if (injury.TryGetComp<HediffComp_GetsPermanent>() is { } permComp)
                permComp.IsPermanent = true;
            pawn.health.AddHediff(injury, part);
        }
        else
        {
            // Fresh bite wound — may sever limb
            float damage = part.def.GetMaxHealth(pawn) * 1.2f;
            pawn.TakeDamage(new DamageInfo(DamageDefOf.Bite, damage, 0f, -1f, null, part));
        }
    }
}
