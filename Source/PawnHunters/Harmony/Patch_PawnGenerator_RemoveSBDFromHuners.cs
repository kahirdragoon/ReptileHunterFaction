using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnHunters;

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new Type[] { typeof(PawnGenerationRequest) })]
public static class PawnGenerator_GeneratePawn_GenesPatch
{
    [HarmonyPostfix]
    public static void RemoveBPFromHunters(Pawn __result)
    {
        if(__result?.Faction == null || __result.Faction.def != PawnHuntersDefOf.PH_PawnHunters)
            return;

        var count = __result.inventory.Count(PawnHuntersDefOf.PH_BP);
        if (count > 0)
            __result.inventory.RemoveCount(PawnHuntersDefOf.PH_BP, count);
    }
}