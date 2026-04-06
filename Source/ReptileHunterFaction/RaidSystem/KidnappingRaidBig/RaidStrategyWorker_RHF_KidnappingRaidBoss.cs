using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Raid strategy worker for the boss kidnapping raid.
/// Identical to the big kidnapping raid but always spawns one guaranteed
/// RHF_ReptileHuntersFighter_Boss on top of the point-scaled force.
/// </summary>
public class RaidStrategyWorker_RHF_KidnappingRaidBoss : RaidStrategyWorker
{
    private const float PointsPerRaider = 250f;

    protected override LordJob MakeLordJob(
        IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
    {
        return new LordJob_RHF_KidnappingRaidBig();
    }

    public override List<Pawn> SpawnThreats(IncidentParms parms)
    {
        // Apply prisoner-gift discount.
        int discount = WorldComp_SpoilsOfBattle.Get()?.ConsumeRaidDiscount() ?? 0;
        if (discount > 0)
            parms.points = Math.Max(def.minPawns * PointsPerRaider, parms.points - discount * PointsPerRaider);

        // Generate the regular point-based raiders.
        PawnGroupMakerParms groupParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(
            PawnGroupKindDefOf.Combat, parms, true);
        List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(groupParms, warnOnZeroResults: true).ToList();

        // Always append one guaranteed boss.
        Pawn boss = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            ReptileHunterFactionDefOf.RHF_ReptileHuntersFighter_Boss,
            parms.faction,
            PawnGenerationContext.NonPlayer,
            tile: (parms.target as Map)?.Tile ?? -1));
        pawns.Add(boss);

        // Spawn all pawns via the raid's configured arrival mode.
        parms.raidArrivalMode.Worker.Arrive(pawns, parms);
        return pawns;
    }
}
