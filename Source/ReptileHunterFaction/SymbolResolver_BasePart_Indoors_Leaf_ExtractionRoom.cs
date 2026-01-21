using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;

public class SymbolResolver_BasePart_Indoors_Leaf_ExtractionRoom : SymbolResolver
{
    public override bool CanResolve(ResolveParams rp)
    {
        return rp.faction.def == ReptileHunterFactionDefOf.RHF_ReptileHunters
            && BaseGen_RHFGlobalSettings.extractionRoomResolved < BaseGen_RHFGlobalSettings.maxExtractionRooms
            && base.CanResolve(rp);
    }

    public override void Resolve(ResolveParams rp)
    {
        BaseGen.symbolStack.Push("rhf_extractionroom", rp);
        ++BaseGen_RHFGlobalSettings.extractionRoomResolved;
    }
}
