using HarmonyLib;
using RimWorld;
using Verse;

namespace ReptileHunterFaction;

/// <summary>
/// Boosts the natural selection weight of <c>RHF_OpportunitySite_KidnappedPawnPrison</c>
/// when the RHF faction is currently holding at least one kidnapped player pawn.
/// </summary>
[HarmonyPatch(typeof(NaturalRandomQuestChooser), nameof(NaturalRandomQuestChooser.GetNaturalRandomSelectionWeight))]
public static class Patch_QuestWeight_RHFPrison
{
    private const string QuestDefName = "RHF_OpportunitySite_KidnappedPawnPrison";

    /// <summary>Multiplier applied to the selection weight when a kidnapped pawn is present.</summary>
    private const float KidnappedPawnWeightMultiplier = 2f;

    [HarmonyPostfix]
    public static void BoostWeightWhenPrisonerExists(QuestScriptDef quest, ref float __result)
    {
        if (__result <= 0f || quest.defName != QuestDefName)
            return;

        Faction rhf = Find.FactionManager.FirstFactionOfDef(ReptileHunterFactionDefOf.RHF_ReptileHunters);
        if (rhf?.kidnapped?.KidnappedPawnsListForReading?.Count > 0)
            __result *= KidnappedPawnWeightMultiplier;
    }
}
