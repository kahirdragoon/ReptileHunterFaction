using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ReptileHunterFaction;

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new Type[] { typeof(PawnGenerationRequest) })]
public static class PawnGenerator_GeneratePawn_GenesPatch
{
    [HarmonyPostfix]
    public static void RemoveSBDFromHunters(Pawn __result)
    {
        if(__result?.Faction == null || __result.Faction.def != ReptileHunterFactionDefOf.RHF_ReptileHunters)
            return;

        var count = __result.inventory.Count(ReptileHunterFactionDefOf.RHF_SBD);
        if (count > 0)
            __result.inventory.RemoveCount(ReptileHunterFactionDefOf.RHF_SBD, count);
    }
}