using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;
public class SymbolResolver_Mortage : SymbolResolver
{
    public override bool CanResolve(ResolveParams rp)
    {
        return false && base.CanResolve(rp);
    }

    public override void Resolve(ResolveParams rp)
    {

    }
}
