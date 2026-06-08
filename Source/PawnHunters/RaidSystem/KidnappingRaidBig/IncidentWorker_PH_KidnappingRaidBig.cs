using System.Linq;
using RimWorld;
using Verse;

namespace PawnHunters;

/// <summary>
/// Incident worker for the big Pawn Hunter kidnapping raid.
/// Fires when:
///   - The Hunter faction exists in the world.
///   - At least one qualifying free colonist, slave, OR prisoner is present
///     (IsTargetPawn filter applies; adult check applies).
/// Raid size is point-based (storyteller assigns parms.points as normal).
/// </summary>
public class IncidentWorker_PH_KidnappingRaidBig : IncidentWorker_RaidEnemy
{
    public override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms)) return false;
        if (parms.target is not Map map) return false;
        if (Find.FactionManager.FirstFactionOfDef(PawnHuntersDefOf.PH_PawnHunters) == null)
            return false;

        bool hasQualifyingColonist = map.mapPawns.FreeColonistsSpawned
            .Concat(map.mapPawns.SlavesOfColonySpawned)
            .Any(p => p.DevelopmentalStage.Adult() && PHPawnTargetingUtility.IsTargetPawn(p));

        bool hasQualifyingPrisoner = map.mapPawns.PrisonersOfColonySpawned
            .Any(p => p.DevelopmentalStage.Adult() && PHPawnTargetingUtility.IsTargetPawn(p));

        return hasQualifyingColonist || hasQualifyingPrisoner;
    }

    public override bool TryResolveRaidFaction(IncidentParms parms)
    {
        parms.faction = Find.FactionManager.FirstFactionOfDef(
            PawnHuntersDefOf.PH_PawnHunters);
        return parms.faction != null;
    }

    public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
    {
        parms.raidStrategy = PawnHuntersDefOf.PH_KidnappingRaidStrategy_Big;
    }
}
