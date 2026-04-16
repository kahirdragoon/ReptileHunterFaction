using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;

public class SymbolResolver_BasePart_Indoors_Leaf_Druglab : SymbolResolver
{
    public override bool CanResolve(ResolveParams rp)
    {
        return rp.faction.def == ReptileHunterFactionDefOf.RHF_ReptileHunters
            && BaseGen_RHFGlobalSettings.druglabsResolved < BaseGen_RHFGlobalSettings.maxDruglabs
            && base.CanResolve(rp);
    }

    public override void Resolve(ResolveParams rp)
    {
        rp.floorDef = VanillaTerrainDefOf.SterileTile;
        BaseGen.symbolStack.Push("rhf_druglab", rp);
        ++BaseGen_RHFGlobalSettings.druglabsResolved;
    }
}
