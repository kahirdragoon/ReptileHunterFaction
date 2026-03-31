using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Noise;

namespace ReptileHunterFaction;

[HarmonyPatch(typeof(FactionIdeosTracker), "ChooseOrGenerateIdeo")]
public static class Patch_ChooseOrGenerateIdeo
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(IdeoGenerator), "GenerateIdeo")]
    public static void ForceStyleOnGeneratedIdeo(Ideo __result, IdeoGenerationParms parms)
    {
        if (parms.forFaction?.defName != ReptileHunterFactionDefOf.RHF_ReptileHunters.defName)
            return;

        AddStyleIfNotPresent(__result, "PSECannibal");
        AddStyleIfNotPresent(__result, "GM_CannibalStyle");

        Log.Message($"Added styles to generated ideo for {parms.forFaction.defName}: {string.Join(", ", __result.thingStyleCategories.Select(c => c.category.defName + ":" + c.priority))}");

        __result.SortStyleCategories();
    }

    private static void AddStyleIfNotPresent(Ideo ideo, string styleName)
    {
        var style = DefDatabase<StyleCategoryDef>.GetNamed(styleName, false);

        if(style == null || ideo.thingStyleCategories.Any(category => category.category == style))
            return;

        var prio = ideo.thingStyleCategories.Count > 0 ? ideo.thingStyleCategories.Max(c => c.priority) + 1f : 1f;
        ideo.thingStyleCategories.Add(new ThingStyleCategoryWithPriority(style, prio));
    }
}