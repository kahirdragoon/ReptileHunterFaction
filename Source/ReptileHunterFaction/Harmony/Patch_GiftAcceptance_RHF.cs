using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace ReptileHunterFaction;

// Allows the player to send drop pod gifts to RHF settlements despite permanentEnemy=true.
// Replicates all non-permanentEnemy conditions from the original check.
[HarmonyPatch(typeof(TransportersArrivalAction_GiveGift), nameof(TransportersArrivalAction_GiveGift.CanGiveGiftTo))]
public static class Patch_TransportersArrivalAction_GiveGift_CanGiveGiftTo_RHF
{
    static void Postfix(IEnumerable<IThingHolder> pods, Settlement settlement,
        ref FloatMenuAcceptanceReport __result)
    {
        if ((bool)__result) return;
        if (settlement?.Faction?.def != ReptileHunterFactionDefOf.RHF_ReptileHunters) return;

        // Replicate the quest lodger check from the original method
        foreach (IThingHolder pod in pods)
        {
            ThingOwner things = pod.GetDirectlyHeldThings();
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Pawn p && p.IsQuestLodger())
                    return;
        }

        if (settlement.Spawned && settlement.Faction != null
            && settlement.Faction != Faction.OfPlayer && !settlement.HasMap)
            __result = (FloatMenuAcceptanceReport)true;
    }
}

// Allows the player to offer caravan gifts to RHF settlements.
// Skips both the permanentEnemy check and the CanTradeNow check (RHF has no TraderKindDef).
[HarmonyPatch(typeof(CaravanArrivalAction_OfferGifts), nameof(CaravanArrivalAction_OfferGifts.CanOfferGiftsTo))]
public static class Patch_CaravanArrivalAction_OfferGifts_CanOfferGiftsTo_RHF
{
    static void Postfix(Caravan caravan, Settlement settlement,
        ref FloatMenuAcceptanceReport __result)
    {
        if ((bool)__result) return;
        if (settlement?.Faction?.def != ReptileHunterFactionDefOf.RHF_ReptileHunters) return;

        Pawn negotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan);
        bool hasNegotiator = negotiator != null
            && !negotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled;

        if (settlement.Spawned && !settlement.HasMap
            && settlement.Faction != null && settlement.Faction != Faction.OfPlayer
            && settlement.Faction.HostileTo(Faction.OfPlayer)
            && hasNegotiator)
            __result = (FloatMenuAcceptanceReport)true;
    }
}
