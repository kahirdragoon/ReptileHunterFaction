using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PawnHunters;

// ── Caravan gift overload ──────────────────────────────────────────────────────

[HarmonyPatch(typeof(FactionGiftUtility), nameof(FactionGiftUtility.GiveGift),
    new Type[] { typeof(List<Tradeable>), typeof(Faction), typeof(GlobalTargetInfo) })]
public static class Patch_FactionGiftUtility_CaravanGift_PH
{
    static void Prefix(List<Tradeable> tradeables, Faction giveTo,
        out (int total, int qualifying) __state)
    {
        __state = (0, 0);
        if (giveTo?.def != PawnHuntersDefOf.PH_PawnHunters) return;

        var prisoners = tradeables
            .Where(t => t.ActionToDo == TradeAction.PlayerSells
                     && t.AnyThing is Pawn { IsPrisoner: true } or Pawn { IsSlave: true })
            .Select(t => (Pawn)t.AnyThing)
            .ToList();

        __state = (prisoners.Count, prisoners.Count(PHPawnTargetingUtility.IsTargetPawn));
    }

    static void Postfix((int total, int qualifying) __state)
        => PHGiftRewardUtility.DeliverRewards(__state.total, __state.qualifying);
}

// ── Drop pod gift overload ─────────────────────────────────────────────────────

[HarmonyPatch(typeof(FactionGiftUtility), nameof(FactionGiftUtility.GiveGift),
    new Type[] { typeof(List<ActiveTransporterInfo>), typeof(Settlement) })]
public static class Patch_FactionGiftUtility_PodGift_PH
{
    static void Prefix(List<ActiveTransporterInfo> pods, Settlement giveTo,
        out (int total, int qualifying) __state)
    {
        __state = (0, 0);
        if (giveTo?.Faction?.def != PawnHuntersDefOf.PH_PawnHunters) return;

        var prisoners = pods
            .SelectMany(pod => pod.innerContainer.OfType<Pawn>())
            .Where(p => p.IsPrisoner || p.IsSlave)
            .ToList();

        __state = (prisoners.Count, prisoners.Count(PHPawnTargetingUtility.IsTargetPawn));
    }

    static void Postfix((int total, int qualifying) __state)
        => PHGiftRewardUtility.DeliverRewards(__state.total, __state.qualifying);
}

// ── Shared delivery logic ──────────────────────────────────────────────────────

internal static class PHGiftRewardUtility
{
    internal static void DeliverRewards(int totalCount, int qualifyingCount)
    {
        if (totalCount <= 0) return;

        Map? homeMap = Find.AnyPlayerHomeMap;
        if (homeMap == null) return;

        Thing drugMed = ThingMaker.MakeThing(PawnHuntersDefOf.PH_DrugMedicine);
        drugMed.stackCount = totalCount;

        // Register qualifying prisoners so the next kidnapping raid is reduced.
        WorldComp_SpoilsOfBattle.Get()?.AddGiftedPrisoners(qualifyingCount);

        IntVec3 dropCell = DropCellFinder.RandomDropSpot(homeMap);
        DropPodUtility.DropThingsNear(dropCell, homeMap, new List<Thing> { drugMed }, canRoofPunch: false, forbid: false);

        string textKey = qualifyingCount > 0
            ? "PH_GiftPayment_LetterText_WithQualifying"
            : "PH_GiftPayment_LetterText_NoQualifying";

        Find.LetterStack.ReceiveLetter(
            "PH_GiftPayment_LetterLabel".Translate(),
            textKey.Translate(
                totalCount.Named("TOTAL"),
                qualifyingCount.Named("QUALIFYING")),
            LetterDefOf.PositiveEvent,
            new LookTargets(new TargetInfo(dropCell, homeMap)));
    }
}
