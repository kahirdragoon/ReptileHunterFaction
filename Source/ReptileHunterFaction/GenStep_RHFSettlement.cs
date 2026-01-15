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
    private static readonly IntRange SettlementSizeRange = new IntRange(50, 50);
    private bool WillPostProcess => this.postProcessSettlementParams != null;

    public override int SeedPart => 0904742260;

    protected override void ScatterAt(IntVec3 c, Map map, GenStepParams parms, int stackCount = 1)
    {
        int randomInRange1 = SettlementSizeRange.RandomInRange;
        int randomInRange2 = SettlementSizeRange.RandomInRange;
        var var = new CellRect(c.x - randomInRange1 / 2, c.z - randomInRange2 / 2, randomInRange1, randomInRange2);
        //Faction faction = this.overrideFaction == null ? (map.ParentFaction == null || map.ParentFaction == Faction.OfPlayer ? Find.FactionManager.RandomEnemyFaction() : map.ParentFaction) : this.overrideFaction;
        Faction faction = Find.FactionManager.FirstFactionOfDef(ReptileHunterFactionDefOf.RHF_ReptileHunters);
        var.ClipInsideMap(map);
        var resolveParams = new ResolveParams()
        {
            sitePart = parms.sitePart,
            rect = var,
            faction = faction,
            settlementDontGeneratePawns = new bool?(!this.generatePawns),
            thingSetMakerDef = this.lootThingSetMaker,
            lootMarketValue = this.lootMarketValue,
            cultivatedPlantDef = ReptileHunterFactionDefOf.RHF_Plant_DrugMedicine,
            edgeDefenseWidth = 4,
            settlementPawnGroupPoints = 7000,
            wallStuff = ThingDefOf.Plasteel,
        };
        if (this.postProcessSettlementParams != null)
            this.postProcessSettlementParams.faction = faction;
        MapGenerator.SetVar<CellRect>("SettlementRect", var);
        BaseGen.globalSettings.map = map;
        BaseGen.globalSettings.minBuildings = 1;
        BaseGen.globalSettings.minBarracks = 1;
        BaseGen.symbolStack.Push("settlement", resolveParams);
        resolveParams.SetCustom(SymbolResolver_MineDefense.MineLayerOffset, 5);
        BaseGen.symbolStack.Push("mineDefense", resolveParams);
        resolveParams.SetCustom(SymbolResolver_MineDefense.MineLayerOffset, 9);
        BaseGen.symbolStack.Push("mineDefense", resolveParams);
        resolveParams.RemoveCustom(SymbolResolver_MineDefense.MineLayerOffset);
        BaseGen.symbolStack.Push("autocannonDefense", resolveParams);
        List<Building> previous = null;
        if (this.WillPostProcess)
            previous = new List<Building>(map.listerThings.GetThingsOfType<Building>());
        BaseGen.Generate();
        if (BaseGen.globalSettings.landingPadsGenerated == 0)
            GenStep_Settlement.GenerateLandingPadNearby(resolveParams.rect, map, faction, out CellRect _);
        if (!this.WillPostProcess)
            return;
        var list = map.listerThings.GetThingsOfType<Building>().Where<Building>(b => !previous.Contains(b)).ToList<Building>();
        previous.Clear();
        MapGenUtility.PostProcessSettlement(map, list, this.postProcessSettlementParams);
    }
}
