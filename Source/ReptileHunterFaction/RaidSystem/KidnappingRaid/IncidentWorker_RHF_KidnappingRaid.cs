using RimWorld;
using Verse;

namespace ReptileHunterFaction;

/// <summary>
/// Incident worker for the Reptile Hunter kidnapping raid.
/// Fires only when:
///   - The Hunter faction exists in the world.
///   - The player colony has between MinColonistCount and MaxColonistCount free colonists.
/// Always uses the Hunter faction and the custom kidnapping raid strategy.
/// </summary>
public class IncidentWorker_RHF_KidnappingRaid : IncidentWorker_RaidEnemy
{
    private const int MinColonistCount = 3;
    private const int MaxColonistCount = 7;

    protected override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms)) return false;
        if (parms.target is not Map map) return false;
        if (Find.FactionManager.FirstFactionOfDef(ReptileHunterFactionDefOf.RHF_ReptileHunters) == null) return false;
        int count = map.mapPawns.FreeAdultColonistsSpawnedCount + map.mapPawns.SlavesOfColonySpawned.Count(s => s.DevelopmentalStage.Adult());
        return count >= MinColonistCount && count <= MaxColonistCount;
    }

    protected override bool TryResolveRaidFaction(IncidentParms parms)
    {
        parms.faction = Find.FactionManager.FirstFactionOfDef(ReptileHunterFactionDefOf.RHF_ReptileHunters);
        return parms.faction != null;
    }

    public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
    {
        parms.raidStrategy = ReptileHunterFactionDefOf.RHF_KidnappingRaidStrategy;
    }


}
