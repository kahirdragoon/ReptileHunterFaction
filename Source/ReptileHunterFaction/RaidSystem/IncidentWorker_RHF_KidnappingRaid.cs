using RimWorld;
using Verse;

namespace ReptileHunterFaction;

/// <summary>
/// Incident worker for the Reptile Hunter kidnapping raid.
/// Fires only when:
///   - The Hunter faction exists in the world.
///   - The player colony has at least MinColonistCount free colonists.
/// Always uses the Hunter faction and the custom kidnapping raid strategy.
/// </summary>
public class IncidentWorker_RHF_KidnappingRaid : IncidentWorker_RaidEnemy
{
    // Minimum number of free colonists required for this raid to fire.
    // Keeps the mechanic from triggering when the player has almost no one to kidnap.
    private const int MinColonistCount = 5;

    protected override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms)) return false;
        if (parms.target is not Map map) return false;
        if (Find.FactionManager.FirstFactionOfDef(ReptileHunterFactionDefOf.RHF_ReptileHunters) == null) return false;
        return map.mapPawns.FreeColonistsCount >= MinColonistCount;
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
