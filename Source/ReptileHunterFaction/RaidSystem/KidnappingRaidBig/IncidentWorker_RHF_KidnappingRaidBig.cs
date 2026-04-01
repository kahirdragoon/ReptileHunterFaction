using System.Linq;
using RimWorld;
using Verse;

namespace ReptileHunterFaction;

/// <summary>
/// Incident worker for the big Reptile Hunter kidnapping raid.
/// Fires when:
///   - The Hunter faction exists in the world.
///   - At least one qualifying free colonist, slave, OR prisoner is present
///     (IsTargetPawn filter applies; adult check applies).
/// Raid size is point-based (storyteller assigns parms.points as normal).
/// </summary>
public class IncidentWorker_RHF_KidnappingRaidBig : IncidentWorker_RaidEnemy
{
    protected override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms)) return false;
        if (parms.target is not Map map) return false;
        if (Find.FactionManager.FirstFactionOfDef(ReptileHunterFactionDefOf.RHF_ReptileHunters) == null)
            return false;

        bool hasQualifyingColonist = map.mapPawns.FreeColonistsSpawned
            .Concat(map.mapPawns.SlavesOfColonySpawned)
            .Any(p => p.DevelopmentalStage.Adult() && RHFPawnTargetingUtility.IsTargetPawn(p));

        bool hasQualifyingPrisoner = map.mapPawns.PrisonersOfColonySpawned
            .Any(p => p.DevelopmentalStage.Adult() && RHFPawnTargetingUtility.IsTargetPawn(p));

        return hasQualifyingColonist || hasQualifyingPrisoner;
    }

    protected override bool TryResolveRaidFaction(IncidentParms parms)
    {
        parms.faction = Find.FactionManager.FirstFactionOfDef(
            ReptileHunterFactionDefOf.RHF_ReptileHunters);
        return parms.faction != null;
    }

    public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
    {
        parms.raidStrategy = ReptileHunterFactionDefOf.RHF_KidnappingRaidStrategy_Big;
    }
}
