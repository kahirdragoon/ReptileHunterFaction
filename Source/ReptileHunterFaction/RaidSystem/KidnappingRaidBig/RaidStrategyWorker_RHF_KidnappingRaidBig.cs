using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Raid strategy worker for the big kidnapping raid.
/// MakeLordJob injects the big kidnapping logic.
/// SpawnThreats applies the prisoner-gift discount to parms.points, then returns null
/// so the vanilla point-based spawning fallback in IncidentWorker_Raid handles generation.
/// </summary>
public class RaidStrategyWorker_RHF_KidnappingRaidBig : RaidStrategyWorker
{
    // Points deducted per prevented raider. Chosen so that at the minimum budget (2000 pts)
    // each "1 raider prevented" is roughly one fighter equivalent.
    private const float PointsPerRaider = 250f;

    protected override LordJob MakeLordJob(
        IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
    {
        return new LordJob_RHF_KidnappingRaidBig();
    }

    public override List<Pawn> SpawnThreats(IncidentParms parms)
    {
        int discount = WorldComp_SpoilsOfBattle.Get()?.ConsumeRaidDiscount() ?? 0;
        if (discount > 0)
            parms.points = Math.Max(def.minPawns * PointsPerRaider, parms.points - discount * PointsPerRaider);

        return null; // Vanilla fallback uses the adjusted parms.points.
    }
}
