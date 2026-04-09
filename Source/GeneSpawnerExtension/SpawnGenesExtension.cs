using System.Collections.Generic;
using Verse;

namespace GeneSpawnerExtension;

public class SpawnGenesExtension : DefModExtension
{
    public bool randomOrder = false;
    public int maxGroupsApplied = -1; // -1 = no limit

    public string? xenotypeNamePrefix;
    public string? xenotypeNameSuffix;
    public string? xenotypeNameReplacement;

    public List<GeneGroup>? groups;
    public MetOffsetGeneConfig? metOffsetGenes;
}

public class GeneGroup : IExposable
{
    public float chance = 1f;
    public IntRange maxGenesApplied = new IntRange(-1, -1); // -1~-1 means no limit
    public bool randomOrder = false;

    // -1 = not set (no gate)
    public float minRaidPoints = -1f;
    public float maxRaidPoints = -1f;
    public float minWealth = -1f;
    public float maxWealth = -1f;
    public float minDay = -1f;
    public float maxDay = -1f;

    public List<GeneSpawnInfo>? genes;
    public MetOffsetGeneConfig? metOffsetGenes;

    public void ExposeData()
    {
        Scribe_Values.Look(ref chance, "chance", 1f);
        Scribe_Values.Look(ref maxGenesApplied, "maxGenesApplied", new IntRange(-1, -1));
        Scribe_Values.Look(ref randomOrder, "randomOrder", false);
        Scribe_Values.Look(ref minRaidPoints, "minRaidPoints", -1f);
        Scribe_Values.Look(ref maxRaidPoints, "maxRaidPoints", -1f);
        Scribe_Values.Look(ref minWealth, "minWealth", -1f);
        Scribe_Values.Look(ref maxWealth, "maxWealth", -1f);
        Scribe_Values.Look(ref minDay, "minDay", -1f);
        Scribe_Values.Look(ref maxDay, "maxDay", -1f);
        Scribe_Collections.Look(ref genes, "genes", LookMode.Deep);
        Scribe_Deep.Look(ref metOffsetGenes, "metOffsetGenes");
    }
}

public class MetOffsetGeneConfig : IExposable
{
    public bool randomOrder = false;
    public List<GeneSpawnInfo>? genes;

    public void ExposeData()
    {
        Scribe_Values.Look(ref randomOrder, "randomOrder", false);
        Scribe_Collections.Look(ref genes, "genes", LookMode.Deep);
    }
}

public class GeneSpawnInfo : IExposable
{
    public string? defName;
    public GeneDef? geneDef; // resolved at runtime, not saved
    public float chance = 1f;
    public bool xenogene = true;

    public void ExposeData()
    {
        Scribe_Values.Look(ref defName, "defName");
        Scribe_Values.Look(ref chance, "chance", 1f);
        Scribe_Values.Look(ref xenogene, "xenogene", true);
    }
}
