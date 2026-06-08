using KCSG;
using RimWorld.BaseGen;
using Verse;

namespace PawnHunters;

/// <summary>
/// Wraps KCSG.GenStep_CustomStructureGen for the kidnapped-pawn prison site.
/// KCSG's symbolResolvers list pushes resolvers onto the BaseGen stack but never calls
/// BaseGen.Generate() for structureLayoutDefs — so the resolvers are never executed.
/// PostGenerate runs after the layout is fully placed and is the correct place to invoke
/// our resolver directly.
/// </summary>
public class GenStep_PH_HuntingHutPrisonGen : GenStep_CustomStructureGen
{
    public override void PostGenerate(CellRect rect, Map map, GenStepParams parms)
    {
        // BaseGen.globalSettings.map is not set by the structureLayoutDef path in KCSG,
        // so set it here before calling any resolver that reads it.
        BaseGen.globalSettings.map = map;

        var rp = new ResolveParams
        {
            faction = map.ParentFaction,
            rect = rect,
        };

        new SymbolResolver_PH_HuntingHutPrison().Resolve(rp);

        BaseGen.globalSettings.map = null;
    }
}
