using RimWorld;
using Verse;

namespace PawnHunters;

public static class PHFactionUtility
{
    /// <summary>
    /// The Pawn Hunter faction while it can still send raiders, or null once it is defeated
    /// (all settlements destroyed) or deactivated — the same states vanilla raids exclude.
    /// </summary>
    public static Faction? RaidingFaction =>
        Find.FactionManager.FirstFactionOfDef(PawnHuntersDefOf.PH_PawnHunters) is { defeated: false, deactivated: false } faction
            ? faction
            : null;
}
