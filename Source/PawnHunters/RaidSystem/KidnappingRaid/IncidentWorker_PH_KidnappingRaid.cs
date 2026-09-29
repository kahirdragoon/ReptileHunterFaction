using RimWorld;
using Verse;

namespace PawnHunters;

/// <summary>
/// Incident worker for the Pawn Hunter kidnapping raid.
/// Fires only when:
///   - The Hunter faction exists in the world.
///   - The player colony has at least PHModSettings.minColonistsForKidnappingRaid adult colonists + adult slaves.
/// Always uses the Hunter faction and the custom kidnapping raid strategy.
/// </summary>
public class IncidentWorker_PH_KidnappingRaid : IncidentWorker_RaidEnemy
{
    /// <summary>Adult free colonists + adult slaves; shared by the fire gate and the raid sizing.</summary>
    public static int CountAdultColonistsAndSlaves(Map map) =>
        map.mapPawns.FreeAdultColonistsSpawnedCount
        + map.mapPawns.SlavesOfColonySpawned.Count(s => s.DevelopmentalStage.Adult());

    public override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms)) return false;
        if (parms.target is not Map map) return false;
        if (Find.FactionManager.FirstFactionOfDef(PawnHuntersDefOf.PH_PawnHunters) == null) return false;
        if (CountAdultColonistsAndSlaves(map) < PawnHuntersMod.Settings.minColonistsForKidnappingRaid) return false;

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
