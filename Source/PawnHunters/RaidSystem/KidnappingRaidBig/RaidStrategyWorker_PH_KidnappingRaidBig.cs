using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace PawnHunters;

/// <summary>
/// Raid strategy worker for the big kidnapping raid.
/// MakeLordJob injects the big kidnapping logic.
/// SpawnThreats applies the prisoner-gift discount to parms.points, then returns null
/// so the vanilla point-based spawning fallback in IncidentWorker_Raid handles generation.
/// </summary>
public class RaidStrategyWorker_PH_KidnappingRaidBig : RaidStrategyWorker
{
    // Points deducted per prevented raider. Chosen so that at the minimum budget (2000 pts)
    // each "1 raider prevented" is roughly one fighter equivalent.
    private const float PointsPerRaider = 250f;

    public override bool CanUseWith(IncidentParms parms, PawnGroupKindDef groupKind) =>
        parms.faction?.def == PawnHuntersDefOf.PH_PawnHunters;

    public override LordJob MakeLordJob(
        IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
    {
        return new LordJob_PH_KidnappingRaidBig();
    }

    public override List<Pawn> SpawnThreats(IncidentParms parms)
    {
        ApplyGiftDiscount(parms, def);
        return null; // Vanilla fallback uses the adjusted parms.points.
    }

    /// <summary>
    /// Lowers parms.points by PointsPerRaider per bought-off raider, but never below minPawns raiders' worth.
    /// Only the raiders that fit above that floor are spent; the remaining prisoners carry over to the next raid.
    /// Shared with the boss raid.
    /// </summary>
    internal static void ApplyGiftDiscount(IncidentParms parms, RaidStrategyDef strategy)
    {
        float minPoints = strategy.minPawns * PointsPerRaider;
        int maxRaiders = (int)Math.Floor((parms.points - minPoints) / PointsPerRaider);
        int discount = WorldComp_SpoilsOfBattle.Get()?.ConsumeRaidDiscount(maxRaiders) ?? 0;
        parms.points -= discount * PointsPerRaider;
    }
}
