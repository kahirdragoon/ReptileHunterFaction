using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;
public class SymbolResolver_AutocannonDefense : SymbolResolver
{
    public override void Resolve(ResolveParams rp)
    {
        Map map = BaseGen.globalSettings.map;
        CellRect rect = rp.rect;

        IntVec3[] positions = new IntVec3[8];

        positions[0] = new IntVec3(rect.minX, 0, rect.maxZ);
        positions[1] = new IntVec3((rect.minX + rect.maxX) / 2, 0, rect.maxZ);
        positions[2] = new IntVec3(rect.maxX, 0, rect.maxZ);

        positions[3] = new IntVec3(rect.maxX, 0, (rect.minZ + rect.maxZ) / 2);
        positions[4] = new IntVec3(rect.maxX, 0, rect.minZ);

        positions[5] = new IntVec3((rect.minX + rect.maxX) / 2, 0, rect.minZ);
        positions[6] = new IntVec3(rect.minX, 0, rect.minZ);

        positions[7] = new IntVec3(rect.minX, 0, (rect.minZ + rect.maxZ) / 2);

        foreach (IntVec3 pos in positions)
        {
            if (pos.InBounds(map) && pos.Standable(map))
            {
                Thing autocannon = ThingMaker.MakeThing(ReptileHunterFactionDefOf.Turret_Autocannon);
                autocannon.SetFaction(rp.faction);
                GenSpawn.Spawn(autocannon, pos, map);
            }
        }
    }
}
