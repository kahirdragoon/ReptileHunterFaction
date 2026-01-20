using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;

public class SymbolResolver_BasePart_Indoors_Leaf_Prison : SymbolResolver
{
    public override bool CanResolve(ResolveParams rp)
    {
        return rp.faction.def == ReptileHunterFactionDefOf.RHF_ReptileHunters 
            && BaseGen_RHFGlobalSettings.prisonsResolved < BaseGen_RHFGlobalSettings.maxPrisons 
            && base.CanResolve(rp);
    }

    public override void Resolve(ResolveParams rp)
    {
        rp.floorDef = TerrainDefOf.Concrete;
        BaseGen.symbolStack.Push("rhf_prison", rp);
        ++BaseGen_RHFGlobalSettings.prisonsResolved;
    }
}
