using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace ReptileHunterFaction;
internal class SitePartWorker_PrisonSite : SitePartWorker
{
    public static readonly SimpleCurve PointsMarketValue = new SimpleCurve()
    {
      {
        new CurvePoint(100f, 300f),
        true
      },
      {
        new CurvePoint(250f, 700),
        true
      },
      {
        new CurvePoint(800f, 5000f),
        true
      },
      {
        new CurvePoint(10000f, 7000f),
        true
      }
    };

    public virtual bool CanSpawnOn(PlanetTile tile) => !Find.WorldGrid[tile].WaterCovered && Find.WorldGrid[tile].hilliness != Hilliness.Impassable;

    public override void Init(Site site, SitePart sitePart)
    {
        base.Init(site, sitePart);

        var loot = LootThings(site.Tile).RandomElementByWeight<CampLootThingStruct>((Func<CampLootThingStruct, float>)(t => t.weight));
        var x = PointsMarketValue.Evaluate(sitePart.parms.threatPoints);
        var thingDefCountList1 = new List<ThingDefCount>();
        sitePart.things = new ThingOwner<Thing>(sitePart);
        sitePart.things.dontTickContents = true;
        var thingDefCountList2 = new List<ThingDefCount>();
        float num = PointsMarketValue.Evaluate(x);
        if (loot.thing2 == null)
        {
            thingDefCountList2.Add(new ThingDefCount(loot.thing, Mathf.CeilToInt(num / loot.thing.BaseMarketValue)));
        }
        else
        {
            thingDefCountList2.Add(new ThingDefCount(loot.thing, Mathf.CeilToInt(num / 2f / loot.thing.BaseMarketValue)));
            thingDefCountList2.Add(new ThingDefCount(loot.thing2, Mathf.CeilToInt(num / 2f / loot.thing2.BaseMarketValue)));
        }
        foreach (ThingDefCount thingDefCount in thingDefCountList2)
        {
            int count = thingDefCount.Count;
            var thingDef = thingDefCount.ThingDef;
            while (count > 0)
            {
                var thing = ThingMaker.MakeThing(thingDef);
                thing.stackCount = Mathf.Min(count, thing.def.stackLimit);
                thingDefCountList1.Add(new ThingDefCount(thingDef, thing.stackCount));
                count -= thing.stackCount;
                sitePart.things.TryAdd(thing);
            }
        }
        sitePart.lootThings = thingDefCountList1;
    }

    public virtual IEnumerable<CampLootThingStruct> LootThings(
      PlanetTile tile)
    {
        foreach (SitePartDef.WorkSiteLootThing workSiteLootThing in def.lootTable)
            yield return new CampLootThingStruct()
            {
                thing = workSiteLootThing.thing,
                weight = workSiteLootThing.weight
            };
    }

    public override string GetArrivedLetterPart(
      Map map,
      out LetterDef preferredLetterDef,
      out LookTargets lookTargets)
    {
        string arrivedLetterPart = base.GetArrivedLetterPart(map, out preferredLetterDef, out lookTargets);
        lookTargets = new LookTargets(map.Parent);
        return arrivedLetterPart;
    }

    public override void Notify_GeneratedByQuestGen(
      SitePart part,
      Slate slate,
      List<Rule> outExtraDescriptionRules,
      Dictionary<string, string> outExtraDescriptionConstants)
    {
        base.Notify_GeneratedByQuestGen(part, slate, outExtraDescriptionRules, outExtraDescriptionConstants);
    }

    public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart) => 
        ((string)(site.Label + ": " 
        + "KnownSiteThreatEnemyCountAppend".Translate("infite", "People".Translate()))).TrimEndNewlines() 
        + ("\n" + "Contains".Translate() + ": " 
        + string.Join(", ", sitePart.lootThings.Select<ThingDefCount, ThingDef>(t => t.ThingDef)
            .Distinct<ThingDef>()
            .Select<ThingDef, string>(t =>
            {
                int num = 0;
                foreach (ThingDefCount lootThing in sitePart.lootThings)
                {
                    if (lootThing.ThingDef == t)
                        num += lootThing.Count;
                }
                return t.LabelCap + " x" + num.ToString();
            })));

    public override SitePartParams GenerateDefaultParams(
      float myThreatPoints,
      PlanetTile tile,
      Faction faction)
    {
        SitePartParams defaultParams = base.GenerateDefaultParams(myThreatPoints, tile, faction);
        defaultParams.threatPoints = Mathf.Max(defaultParams.threatPoints, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Settlement));
        return defaultParams;
    }

    public override bool FactionCanOwn(Faction faction)
    {
        if (faction.Hidden || faction.temporary)
            return false;
        return faction.def.defName == "RHF_ReptileHunters";
    }

    protected string GetEnemiesLabel(Site site, int enemiesCount)
    {
        if (site.Faction == null)
            return (string)(enemiesCount == 1 ? "Enemy".Translate() : "Enemies".Translate());
        return enemiesCount != 1 ? site.Faction.def.pawnsPlural : site.Faction.def.pawnSingular;
    }

    public struct CampLootThingStruct
    {
        public ThingDef thing;
        public ThingDef thing2;
        public float weight;
    }
}
