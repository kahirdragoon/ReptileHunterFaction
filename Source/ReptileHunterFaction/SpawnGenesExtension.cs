using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;
internal class SpawnGenesExtension : DefModExtension
{
    public List<GeneSpawnInfo>? genes;
    public bool randomOrder;
    public bool respectMetabolicEfficiency;
    public int? maxNumberOfGenesAdded;
}

internal class GeneSpawnInfo
{
    public string? defName;
    public GeneDef? geneDef;
    public float chance = 1;
    public bool xenogene = true;
}