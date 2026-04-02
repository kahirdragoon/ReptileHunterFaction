using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace ReptileHunterFaction;

public class RaidStrategyWorker_RHF_KidnappingRaid : RaidStrategyWorker
{
    protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
    {
        return new LordJob_RHF_KidnappingRaid();
    }

    /// <summary>
    /// Spawns exactly (free colonists - 1) raiders using the faction's configured
    /// pawnGroupMakers, picking options by their selectionWeight just like vanilla does —
    /// but for a fixed count instead of a points budget.
    /// </summary>
    public override List<Pawn> SpawnThreats(IncidentParms parms)
    {
        Map map = (Map)parms.target;
        int discount = WorldComp_SpoilsOfBattle.Get()?.ConsumeRaidDiscount() ?? 0;
        int count = map.mapPawns.FreeColonistsCount - 2 - discount;
        if (count <= 0) return null;

        // Build standard group-maker parms; use a high points value so CanGenerateFrom passes.
        PawnGroupMakerParms groupParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(
            PawnGroupKindDefOf.Combat, parms);
        groupParms.points = 10000f;

        if (!PawnGroupMakerUtility.TryGetRandomPawnGroupMaker(groupParms, out PawnGroupMaker groupMaker))
            return null;

        var pawns = new List<Pawn>(count);
        for (int i = 0; i < count; i++)
        {
            if (!groupMaker.options.TryRandomElementByWeight(o => o.selectionWeight, out PawnGenOption opt))
                continue;

            pawns.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                opt.kind,
                parms.faction,
                mustBeCapableOfViolence: true,
                allowFood: def.pawnsCanBringFood)));
        }

        if (pawns.Count == 0) return null;

        parms.raidArrivalMode.Worker.Arrive(pawns, parms);
        return pawns;
    }
}
