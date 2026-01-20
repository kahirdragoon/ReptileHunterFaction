using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;
public class SymbolResolver_Interior_Prison : SymbolResolver
{
    public override void Resolve(ResolveParams rp)
    {
        var foodStacksCount = (int)((rp.rect.Count() / 6) * Rand.Range(0.5f, 1.5f));
        var cells = rp.rect.TakeRandomDistinct(foodStacksCount);

        foreach (var cell in cells)
        {
            var rpFood = rp with
            {
                rect = CellRect.SingleCell(cell),
                singleThingDef = Rand.Chance(0.5f) ? ThingDefOf.Meat_Human : ReptileHunterFactionDefOf.Meat_Megaspider,
                singleThingStackCount = Rand.Range(3, 10)
            };
            Log.Message("Category of thing: " + rpFood.singleThingDef.category);
            BaseGen.symbolStack.Push("thing", rpFood);
        }
        InteriorSymbolResolverUtility.PushBedroomHeatersCoolersAndLightSourcesSymbols(rp, false);
        var prevPostThingSpawn = rp.postThingSpawn;
        var rpBeds = rp with
        {
            singleThingDef = ThingDefOf.SleepingSpot,
            postThingSpawn = (x) =>
            {
                if (prevPostThingSpawn != null)
                    prevPostThingSpawn(x);
                if (x is Building_Bed bed)
                    bed.ForPrisoners = true;
            }
        };
        BaseGen.symbolStack.Push("fillWithBeds", rpBeds);
    }
}
