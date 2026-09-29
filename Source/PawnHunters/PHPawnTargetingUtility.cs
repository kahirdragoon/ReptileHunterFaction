using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PawnHunters;

[StaticConstructorOnStartup]
public static class PHPawnTargetingUtility
{
    private static List<GeneDef>     _cachedGenes     = [];
    private static List<XenotypeDef> _cachedXenotypes = [];

    // Runs after all defs are loaded; the Mod constructor is too early (the DefDatabase is still empty there).
    static PHPawnTargetingUtility() => RebuildCache();

    /// <summary>
    /// Resolves defNames from settings into cached def references.
    /// Runs once on startup and again whenever the settings are saved.
    /// </summary>
    public static void RebuildCache()
    {
        var s = PawnHuntersMod.Settings;
        _cachedGenes = s.targetGenes
            .Select(n => DefDatabase<GeneDef>.GetNamed(n, errorOnFail: false))
            .Where(d => d != null)
            .ToList()!;
        _cachedXenotypes = s.targetXenotypes
            .Select(n => DefDatabase<XenotypeDef>.GetNamed(n, errorOnFail: false))
            .Where(d => d != null)
            .ToList()!;
    }

    /// <summary>
    /// Returns true if the pawn should be treated as a kidnap/extraction target.
    /// If no xenotypes or genes are configured, all pawns qualify.
    /// </summary>
    public static bool IsTargetPawn(Pawn pawn)
    {
        // Nothing configured → target everyone
        if (_cachedXenotypes.Count == 0 && _cachedGenes.Count == 0)
            return true;

        if (pawn?.genes == null) return false;

        // Xenotype check (any match in the configured list)
        if (_cachedXenotypes.Count > 0 && pawn.genes.Xenotype != null
            && _cachedXenotypes.Contains(pawn.genes.Xenotype))
            return true;

        // Gene check (AND or OR depending on setting). A plain loop: the raid toils call this every 60 ticks
        // per candidate, and the LINQ version allocated a closure each call.
        if (_cachedGenes.Count > 0)
        {
            bool requireAll = PawnHuntersMod.Settings.geneMatchRequiresAll;
            foreach (GeneDef gene in _cachedGenes)
            {
                // AND: the first missing gene fails. OR: the first present gene passes.
                if (pawn.genes.HasActiveGene(gene) != requireAll)
                    return !requireAll;
            }
            return requireAll;
        }

        return false;
    }
}
