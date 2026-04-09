using System.Collections.Generic;
using Verse;

namespace GeneSpawnerExtension;

/// <summary>
/// Player-configured gene spawn settings for a specific faction or pawnkind def.
/// Mirrors SpawnGenesExtension fields but uses IExposable for Scribe serialization.
/// </summary>
public class DefGeneSpawnConfig : IExposable
{
    public string defName = "";
    public bool randomOrder = false;
    public int maxGroupsApplied = -1; // -1 = no limit
    public string? xenotypeNamePrefix;
    public string? xenotypeNameSuffix;
    public string? xenotypeNameReplacement;
    public List<GeneGroup> groups = [];          // shared GeneGroup class
    public MetOffsetGeneConfig? metOffsetGenes;  // shared class

    public void ExposeData()
    {
        Scribe_Values.Look(ref defName, "defName", "");
        Scribe_Values.Look(ref randomOrder, "randomOrder", false);
        Scribe_Values.Look(ref maxGroupsApplied, "maxGroupsApplied", -1);
        Scribe_Values.Look(ref xenotypeNamePrefix, "xenotypeNamePrefix");
        Scribe_Values.Look(ref xenotypeNameSuffix, "xenotypeNameSuffix");
        Scribe_Values.Look(ref xenotypeNameReplacement, "xenotypeNameReplacement");
        Scribe_Collections.Look(ref groups, "groups", LookMode.Deep);
        Scribe_Deep.Look(ref metOffsetGenes, "metOffsetGenes");
        groups ??= [];
    }
}

public class GeneSpawnerSettings : ModSettings
{
    public List<DefGeneSpawnConfig> factionConfigs = [];
    public List<DefGeneSpawnConfig> pawnKindConfigs = [];

    private Dictionary<string, DefGeneSpawnConfig>? _factionCache;
    private Dictionary<string, DefGeneSpawnConfig>? _pawnKindCache;

    public DefGeneSpawnConfig? GetFactionConfig(string? defName)
    {
        if (defName == null) return null;
        _factionCache ??= BuildCache(factionConfigs);
        return _factionCache.GetValueOrDefault(defName);
    }

    public DefGeneSpawnConfig? GetPawnKindConfig(string? defName)
    {
        if (defName == null) return null;
        _pawnKindCache ??= BuildCache(pawnKindConfigs);
        return _pawnKindCache.GetValueOrDefault(defName);
    }

    public void InvalidateCache()
    {
        _factionCache = null;
        _pawnKindCache = null;
    }

    private static Dictionary<string, DefGeneSpawnConfig> BuildCache(List<DefGeneSpawnConfig> list)
    {
        var dict = new Dictionary<string, DefGeneSpawnConfig>(list.Count);
        foreach (var cfg in list)
            if (!cfg.defName.NullOrEmpty())
                dict[cfg.defName] = cfg;
        return dict;
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref factionConfigs, "factionConfigs", LookMode.Deep);
        Scribe_Collections.Look(ref pawnKindConfigs, "pawnKindConfigs", LookMode.Deep);
        factionConfigs ??= [];
        pawnKindConfigs ??= [];
        InvalidateCache();
    }
}
