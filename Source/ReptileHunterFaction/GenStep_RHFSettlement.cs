using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;
internal class GenStep_RHFSettlement : GenStep_Settlement
{
    private static readonly IntRange SettlementSizeRange = new(40, 60);
    private bool WillPostProcess => postProcessSettlementParams != null;

    public override int SeedPart => 0904742260;

    protected override void ScatterAt(IntVec3 c, Map map, GenStepParams parms, int stackCount = 1)
    {
        int randomInRange1 = SettlementSizeRange.RandomInRange;
        int randomInRange2 = SettlementSizeRange.RandomInRange;
        var var = new CellRect(c.x - randomInRange1 / 2, c.z - randomInRange2 / 2, randomInRange1, randomInRange2);
        //Faction faction = overrideFaction == null ? (map.ParentFaction == null || map.ParentFaction == Faction.OfPlayer ? Find.FactionManager.RandomEnemyFaction() : map.ParentFaction) : overrideFaction;
        Faction faction = Find.FactionManager.FirstFactionOfDef(ReptileHunterFactionDefOf.RHF_ReptileHunters);
        var.ClipInsideMap(map);
        var resolveParams = new ResolveParams()
        {
            sitePart = parms.sitePart,
            rect = var,
            faction = faction,
            settlementDontGeneratePawns = !generatePawns,
            thingSetMakerDef = lootThingSetMaker,
            lootMarketValue = lootMarketValue,
            cultivatedPlantDef = ReptileHunterFactionDefOf.RHF_Plant_DrugMedicine,
            edgeDefenseWidth = 4,
            settlementPawnGroupPoints = 10000,
        };
        postProcessSettlementParams?.faction = faction;
        MapGenerator.SetVar("SettlementRect", var);
        BaseGen.globalSettings.map = map;
        BaseGen.globalSettings.minBuildings = 1;
        BaseGen.globalSettings.minBarracks = 1;
        BaseGen_RHFGlobalSettings.maxPrisons = Rand.Range(1, 3);
        BaseGen_RHFGlobalSettings.maxExtractionRooms = 1;
        BaseGen_RHFGlobalSettings.maxDruglabs = 1;
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
        if (!WillPostProcess)
            return;
        var list = map.listerThings.GetThingsOfType<Building>().Where(b => !previous!.Contains(b)).ToList();
        previous!.Clear();
        MapGenUtility.PostProcessSettlement(map, list, postProcessSettlementParams);
    }
}
