using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace PawnHunters;

public class RaidStrategyWorker_PH_KidnappingRaid : RaidStrategyWorker
{
    public override bool CanUseWith(IncidentParms parms, PawnGroupKindDef groupKind) =>
        parms.faction?.def == PawnHuntersDefOf.PH_PawnHunters;

    public override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
    {
        return new LordJob_PH_KidnappingRaid();
    }

    /// <summary>
    /// Spawns exactly floor((adult colonists + adult slaves) / 2) raiders using the faction's configured
    /// pawnGroupMakers, picking options by their selectionWeight just like vanilla does —
    /// but for a fixed count instead of a points budget. The storyteller points only choose
    /// the group maker (and with it the tier mix), not the raider count.
    /// </summary>
    public override List<Pawn> SpawnThreats(IncidentParms parms)
    {
        Map map = (Map)parms.target;
        int discount = WorldComp_SpoilsOfBattle.Get()?.ConsumeRaidDiscount() ?? 0;
        int count = IncidentWorker_PH_KidnappingRaid.CountAdultColonistsAndSlaves(map) / 2 - discount;
        if (count <= 0) return null;

        PawnGroupMaker? groupMaker = GetBracketGroupMaker(parms.faction.def, parms.points);
        if (groupMaker == null) return null;

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

    /// <summary>
    /// Picks the Combat group maker whose points bracket contains <paramref name="points"/>: the one with the
    /// lowest maxTotalPoints that is still >= points, so each group covers (previous group's max, its own max].
    /// Unlike vanilla, higher groups are not eligible at lower points. Commonality-0 groups (boss only) are skipped;
    /// groups sharing the same max are picked by commonality.
    /// </summary>
    private static PawnGroupMaker? GetBracketGroupMaker(FactionDef faction, float points)
    {
        List<PawnGroupMaker> makers = faction.pawnGroupMakers;
        if (makers == null) return null;

        float bracketMax = float.MaxValue;
        foreach (PawnGroupMaker gm in makers)
        {
            if (IsCombatCandidate(gm) && gm.maxTotalPoints >= points && gm.maxTotalPoints < bracketMax)
                bracketMax = gm.maxTotalPoints;
        }
        if (bracketMax == float.MaxValue) return null;

        return makers
            .Where(gm => IsCombatCandidate(gm) && gm.maxTotalPoints == bracketMax)
            .TryRandomElementByWeight(gm => gm.commonality, out PawnGroupMaker result)
            ? result
            : null;
    }

    private static bool IsCombatCandidate(PawnGroupMaker gm) =>
        gm.kindDef == PawnGroupKindDefOf.Combat && gm.commonality > 0f;
}
