using RimWorld;
using Verse;

namespace PawnHunters;

/// <summary>
/// Incident worker for the boss kidnapping raid.
/// Same fire conditions and faction resolution as the big raid, plus a configurable
/// minimum free-colonist count gate before the greatest hunter will bother showing up.
/// </summary>
public class IncidentWorker_PH_KidnappingRaidBoss : IncidentWorker_PH_KidnappingRaidBig
{
    public override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms)) return false;
        if (parms.target is not Map map) return false;
        return map.mapPawns.FreeColonistsSpawned.Count >= PawnHuntersMod.Settings.minPawnsForBossRaid;
    }

    public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
    {
        parms.raidStrategy = PawnHuntersDefOf.PH_KidnappingRaidStrategy_Boss;
    }
}
