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
}
