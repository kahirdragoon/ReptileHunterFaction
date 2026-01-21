using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;

[DefOf]
public static class VanillaTerrainDefOf
{
    public static TerrainDef SterileTile;

    static VanillaTerrainDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(VanillaTerrainDefOf));
}
