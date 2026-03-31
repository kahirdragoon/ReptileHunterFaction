using System.Collections.Generic;
using Verse;

namespace ReptileHunterFaction;

public class RHFModSettings : ModSettings
{
    public List<string> targetXenotypes = [];
    public List<string> targetGenes = [];
    public bool geneMatchRequiresAll = false;
    public int minQualifyingPawns = 1;

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref targetXenotypes, "targetXenotypes", LookMode.Value);
        Scribe_Collections.Look(ref targetGenes, "targetGenes", LookMode.Value);
        Scribe_Values.Look(ref geneMatchRequiresAll, "geneMatchRequiresAll", false);
        Scribe_Values.Look(ref minQualifyingPawns, "minQualifyingPawns", 1);
        targetXenotypes ??= [];
        targetGenes ??= [];
    }
}
