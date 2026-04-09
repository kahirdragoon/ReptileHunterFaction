using System.Collections.Generic;
using Verse;

namespace GeneSpawnerExtension;

public class SpawnGenesExtension : DefModExtension
{
    public bool randomOrder = false;
    public int? maxGroupsApplied = null;

    public string? xenotypeNamePrefix;
    public string? xenotypeNameSuffix;
    public string? xenotypeNameReplacement;

    public List<GeneGroup>? groups;
    public MetOffsetGeneConfig? metOffsetGenes;
}

public class GeneGroup
{
    public float chance = 1f;
    public IntRange maxGenesApplied = new IntRange(-1, -1); // -1~-1 means no limit
    public bool randomOrder = false;

    public float? minRaidPoints = null;
    public float? maxRaidPoints = null;
    public float? minWealth = null;
    public float? maxWealth = null;
    public float? minDay = null;
    public float? maxDay = null;

    public List<GeneSpawnInfo>? genes;
    public MetOffsetGeneConfig? metOffsetGenes;
}

public class MetOffsetGeneConfig
{
    public bool randomOrder = false;
    public List<GeneSpawnInfo>? genes;
}

public class GeneSpawnInfo
{
    public string? defName;
    public GeneDef? geneDef;
    public float chance = 1f;
    public bool xenogene = true;
}
