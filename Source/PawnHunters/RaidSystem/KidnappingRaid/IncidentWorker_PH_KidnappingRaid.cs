using RimWorld;
using Verse;

namespace PawnHunters;

/// <summary>
/// Incident worker for the Pawn Hunter kidnapping raid.
/// Fires only when:
///   - The Hunter faction exists in the world.
///   - The player colony has between MinColonistCount and MaxColonistCount free colonists.
/// Always uses the Hunter faction and the custom kidnapping raid strategy.
/// </summary>
public class IncidentWorker_PH_KidnappingRaid : IncidentWorker_RaidEnemy
{
    private const int MinColonistCount = 3;
    private const int MaxColonistCount = 7;

    public override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms)) return false;
        if (parms.target is not Map map) return false;
        if (Find.FactionManager.FirstFactionOfDef(PawnHuntersDefOf.PH_PawnHunters) == null) return false;
        int count = map.mapPawns.FreeAdultColonistsSpawnedCount + map.mapPawns.SlavesOfColonySpawned.Count(s => s.DevelopmentalStage.Adult());
        if (count < MinColonistCount || count > MaxColonistCount) return false;

        int qualifying = map.mapPawns.FreeColonistsAndPrisonersSpawned
            .Concat(map.mapPawns.SlavesOfColonySpawned)
            .Count(p => p.DevelopmentalStage.Adult() && PHPawnTargetingUtility.IsTargetPawn(p));
        return qualifying >= PawnHuntersMod.Settings.minQualifyingPawns;
    }

    public override bool TryResolveRaidFaction(IncidentParms parms)
    {
        parms.faction = Find.FactionManager.FirstFactionOfDef(PawnHuntersDefOf.PH_PawnHunters);
        return parms.faction != null;
    }

    public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
    {
        parms.raidStrategy = PawnHuntersDefOf.PH_KidnappingRaidStrategy;
    }


}
