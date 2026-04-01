using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Raid strategy worker for the big kidnapping raid.
/// SpawnThreats is NOT overridden — the vanilla fallback in IncidentWorker_RaidEnemy
/// uses parms.points (set by the storyteller) for standard point-based spawning.
/// Only MakeLordJob is overridden to inject the big kidnapping raid logic.
/// </summary>
public class RaidStrategyWorker_RHF_KidnappingRaidBig : RaidStrategyWorker
{
    protected override LordJob MakeLordJob(
        IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
    {
        return new LordJob_RHF_KidnappingRaidBig();
    }
}
