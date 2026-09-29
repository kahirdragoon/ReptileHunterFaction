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
    /// <summary>
    /// Adult free colonists + adult slaves; shared by the fire gate and the raid sizing.
    /// FreeColonistsSpawned already includes slaves (a slave's HostFaction is null), so they must not be added again.
    /// </summary>
    public static int CountAdultColonistsAndSlaves(Map map) => map.mapPawns.FreeAdultColonistsSpawnedCount;

    /// <summary>Raider count before the gifted-prisoner discount: floor((adult colonists + adult slaves) / 2).</summary>
    public static int RaidSize(Map map) => CountAdultColonistsAndSlaves(map) / 2;

    public override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms)) return false;
        if (parms.target is not Map map) return false;
        if (PHFactionUtility.RaidingFaction == null) return false;
        if (CountAdultColonistsAndSlaves(map) < PawnHuntersMod.Settings.minColonistsForKidnappingRaid) return false;
        // At least one raider before the discount; a single colonist would give floor(1 / 2) = 0.
        if (RaidSize(map) < 1) return false;

        // Slaves are already part of the free colonists.
        int qualifying = map.mapPawns.FreeColonistsAndPrisonersSpawned
            .Count(p => p.DevelopmentalStage.Adult() && PHPawnTargetingUtility.IsTargetPawn(p));
        return qualifying >= PawnHuntersMod.Settings.minQualifyingPawns;
    }

    /// <summary>
    /// When the gifted-prisoner discount covers the whole raid, the raid is paid off: it counts as fired,
    /// spends only the prisoners that cover this raid (the rest carry over) and sends nobody.
    /// Otherwise the normal raid runs and SpawnThreats spends the discount.
    /// </summary>
    public override bool TryExecuteWorker(IncidentParms parms)
    {
        WorldComp_SpoilsOfBattle? spoils = WorldComp_SpoilsOfBattle.Get();
        if (parms.target is Map map && spoils != null && spoils.PeekRaidDiscount() >= RaidSize(map))
        {
            spoils.ConsumeRaidDiscount(RaidSize(map));
            Messages.Message("PH_KidnappingRaidPaidOff".Translate(), MessageTypeDefOf.PositiveEvent);
            return true;
        }
        return base.TryExecuteWorker(parms);
    }

    public override bool TryResolveRaidFaction(IncidentParms parms)
    {
        parms.faction = PHFactionUtility.RaidingFaction;
        return parms.faction != null;
    }

    public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
    {
        parms.raidStrategy = PawnHuntersDefOf.PH_KidnappingRaidStrategy;
    }
}
