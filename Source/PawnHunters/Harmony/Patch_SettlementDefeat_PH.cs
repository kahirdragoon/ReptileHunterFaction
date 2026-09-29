using HarmonyLib;
using RimWorld;
using RimWorld.Planet;

namespace PawnHunters;

/// <summary>
/// CheckDefeated is where vanilla sets Faction.defeated, when the faction's last settlement falls.
/// Once that happens to the Pawn Hunters, their stored raid corpses can never be placed anymore and are discarded.
/// The faction is read in the prefix because the method destroys the settlement.
/// </summary>
[HarmonyPatch(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated))]
public static class Patch_SettlementDefeat_PH
{
    public static void Prefix(Settlement factionBase, out Faction? __state) => __state = factionBase.Faction;

    public static void Postfix(Faction? __state)
    {
        if (__state is { defeated: true } && __state.def == PawnHuntersDefOf.PH_PawnHunters)
            WorldComp_SpoilsOfBattle.Get()?.TryDiscardCorpsesAfterDefeat();
    }
}
