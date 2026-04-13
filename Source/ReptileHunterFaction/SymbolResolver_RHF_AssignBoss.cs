using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace ReptileHunterFaction;

// Called via KCSG's kcsg_runresolvers after SettlementGenUtils.Generate has already
// placed all structures (including the PH_ThroneRoom layout with its throne).
// Spawns the boss pawnkind near the throne and assigns the throne to them.
public class SymbolResolver_RHF_AssignBoss : SymbolResolver
{
    public override void Resolve(ResolveParams rp)
    {
        Map map = BaseGen.globalSettings.map;

        Building_Throne throne = map.listerThings.ThingsOfDef(ThingDefOf.Throne)
            .OfType<Building_Throne>()
            .FirstOrDefault();

        if (throne == null)
            return;

        if (!CellFinder.TryFindRandomCellNear(throne.Position, map, 3,
                c => c.Standable(map) && !c.Fogged(map), out IntVec3 spawnCell))
            return;

        BaseGen.symbolStack.Push("pawn", rp with
        {
            singlePawnKindDef = ReptileHunterFactionDefOf.RHF_ReptileHuntersFighter_Boss,
            rect = CellRect.SingleCell(spawnCell),
            postThingSpawn = t =>
            {
                if (t is Pawn boss)
                    boss.ownership.ClaimThrone(throne);
            }
        });
    }
}
