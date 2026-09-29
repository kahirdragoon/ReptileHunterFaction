using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PawnHunters;
internal class GenStep_PHSettlement : GenStep_Settlement
{
    private static readonly IntRange SettlementSizeRange = new(40, 60);
    private bool WillPostProcess => postProcessSettlementParams != null;

    public override int SeedPart => 0904742260;

    public override void ScatterAt(IntVec3 c, Map map, GenStepParams parms, int stackCount = 1)
    {
        int randomInRange1 = SettlementSizeRange.RandomInRange;
        int randomInRange2 = SettlementSizeRange.RandomInRange;
        var var = new CellRect(c.x - randomInRange1 / 2, c.z - randomInRange2 / 2, randomInRange1, randomInRange2);
        Faction faction = overrideFaction ?? (map.ParentFaction == null || map.ParentFaction == Faction.OfPlayer ? Find.FactionManager.RandomEnemyFaction() : map.ParentFaction);
        var.ClipInsideMap(map);
        var resolveParams = new ResolveParams()
        {
            sitePart = parms.sitePart,
            rect = var,
            faction = faction,
            settlementDontGeneratePawns = !generatePawns,
            thingSetMakerDef = lootThingSetMaker,
            lootMarketValue = lootMarketValue,
            cultivatedPlantDef = PawnHuntersDefOf.PH_Plant_DrugMedicine,
            edgeDefenseWidth = 4,
            settlementPawnGroupPoints = 10000,
        };
        postProcessSettlementParams?.faction = faction;
        MapGenerator.SetVar("SettlementRect", var);
        BaseGen.globalSettings.map = map;
        BaseGen.globalSettings.minBuildings = 1;
        BaseGen.globalSettings.minBarracks = 1;
        BaseGen_PHGlobalSettings.maxPrisons = Rand.Range(1, 3);
        BaseGen_PHGlobalSettings.maxExtractionRooms = 1;
        BaseGen_PHGlobalSettings.maxDruglabs = 1;
        BaseGen.symbolStack.Push("settlement", resolveParams);
        resolveParams.SetCustom(SymbolResolver_MineDefense.MineLayerOffset, 5);
        BaseGen.symbolStack.Push("rhf_mineDefense", resolveParams);
        resolveParams.SetCustom(SymbolResolver_MineDefense.MineLayerOffset, 10);
        BaseGen.symbolStack.Push("rhf_mineDefense", resolveParams);
        resolveParams.RemoveCustom(SymbolResolver_MineDefense.MineLayerOffset);
        BaseGen.symbolStack.Push("rhf_autocannonDefense", resolveParams);
        List<Building>? previous = null;
        if (WillPostProcess)
            previous = [.. map.listerThings.GetThingsOfType<Building>()];
        BaseGen.Generate();
        if (BaseGen.globalSettings.landingPadsGenerated == 0)
            GenerateLandingPadNearby(resolveParams.rect, map, faction, out CellRect _);
        TrySpawnScannerInSettlement(map, var, faction);

        if (!WillPostProcess)
            return;
        var list = map.listerThings.GetThingsOfType<Building>().Where(b => !previous!.Contains(b)).ToList();
        previous!.Clear();
        MapGenUtility.PostProcessSettlement(map, list, postProcessSettlementParams);
    }

    private static void TrySpawnScannerInSettlement(Map map, CellRect settlementRect, Faction faction)
    {
        ThingDef? scannerDef = DefDatabase<ThingDef>.GetNamedSilentFail("LongRangeAncientComplexScanner");
        if (scannerDef == null)
            return;

        // Search the settlement rect for a clear, unroofed footprint.
        // Shuffle candidate positions so we don't always prefer the same corner.
        List<IntVec3> candidates = [.. settlementRect.Cells];
        candidates.Shuffle();

        foreach (IntVec3 pos in candidates)
        {
            // A building's position is the center of its footprint (GenAdj.OccupiedRect), not a corner.
            CellRect footprint = GenAdj.OccupiedRect(pos, Rot4.South, scannerDef.size);
            if (!footprint.FullyContainedWithin(settlementRect) || !CanPlaceUnroofed(map, footprint))
                continue;

            Thing scanner = ThingMaker.MakeThing(scannerDef);
            if (scannerDef.CanHaveFaction)
                scanner.SetFaction(faction);
            GenSpawn.Spawn(scanner, pos, map, Rot4.South);
            return;
        }
    }

    private static bool CanPlaceUnroofed(Map map, CellRect footprint)
    {
        foreach (IntVec3 cell in footprint)
        {
            if (!cell.InBounds(map))
                return false;
            if (cell.GetTerrain(map).passability == Traversability.Impassable)
                return false;
            if (cell.GetEdifice(map) != null)
                return false;
            if (cell.Roofed(map))
                return false;
        }
        return true;
    }
}
