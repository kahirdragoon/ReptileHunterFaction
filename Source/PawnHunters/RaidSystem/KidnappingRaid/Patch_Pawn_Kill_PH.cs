using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace PawnHunters;

/// <summary>
/// Notifies LordJob_PH_KidnappingRaid whenever one of its raiders kills a player pawn,
/// so that raider can later extract the skull as a trophy.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Patch_Pawn_Kill_PH
{
    public static void Postfix(Pawn __instance, DamageInfo? dinfo)
    {
        // Only care about humanlike player pawns dying.
        if (!__instance.RaceProps.Humanlike || __instance.Faction != Faction.OfPlayer) return;

        Pawn? killer = dinfo.HasValue ? dinfo.Value.Instigator as Pawn : null;
        if (killer == null || killer.Faction?.def != PawnHuntersDefOf.PH_PawnHunters) return;

        (killer.GetLord()?.LordJob as LordJob_PH_KidnappingRaid)?.RegisterKill(killer, __instance);
    }
}
