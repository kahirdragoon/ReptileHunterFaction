using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PawnHunters;

public class SymbolResolver_BasePart_Indoors_Leaf_ExtractionRoom : SymbolResolver
{
    public override bool CanResolve(ResolveParams rp)
    {
        return rp.faction.def == PawnHuntersDefOf.PH_PawnHunters
            && BaseGen_PHGlobalSettings.extractionRoomResolved < BaseGen_PHGlobalSettings.maxExtractionRooms
            && base.CanResolve(rp);
    }

    public override void Resolve(ResolveParams rp)
    {
        BaseGen.symbolStack.Push("rhf_extractionroom", rp);
        ++BaseGen_PHGlobalSettings.extractionRoomResolved;
    }
}
