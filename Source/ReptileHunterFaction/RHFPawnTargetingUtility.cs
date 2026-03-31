using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ReptileHunterFaction;

public static class RHFPawnTargetingUtility
{
    private static List<GeneDef>     _cachedGenes     = [];
    private static List<XenotypeDef> _cachedXenotypes = [];

    /// <summary>
    /// Resolves defNames from settings into cached def references.
    /// Call once on startup and again whenever settings change.
    /// </summary>
    public static void RebuildCache()
    {
        var s = ReptileHunterFactionMod.Settings;
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

        // Gene check (AND or OR depending on setting)
        if (_cachedGenes.Count > 0)
        {
            return ReptileHunterFactionMod.Settings.geneMatchRequiresAll
                ? _cachedGenes.All(g => pawn.genes.HasActiveGene(g))
                : _cachedGenes.Any(g => pawn.genes.HasActiveGene(g));
        }

        return false;
    }
}
