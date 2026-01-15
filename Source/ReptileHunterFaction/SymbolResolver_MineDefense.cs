using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;
public class SymbolResolver_MineDefense : SymbolResolver
{
    public const string MineLayerOffset = "MineLayerOffset";

    public override void Resolve(ResolveParams rp)
    {
        Map map = BaseGen.globalSettings.map;
        var offset = rp.GetCustom<int>(MineLayerOffset);
        CellRect rect = rp.rect with
        {
            minX = rp.rect.minX - offset,
            minZ = rp.rect.minZ - offset,
            maxX = rp.rect.maxX + offset,
            maxZ = rp.rect.maxZ + offset
        };

        // List to hold positions
        List<IntVec3> positions = new List<IntVec3>();

        // Step size for spacing
        int step = 2;

        // Top edge (left to right)
        for (int x = rect.minX; x <= rect.maxX; x += step)
        {
            positions.Add(new IntVec3(x, 0, rect.maxZ));
        }

        // Right edge (top to bottom)
        for (int z = rect.maxZ - step; z >= rect.minZ; z -= step)
        {
            positions.Add(new IntVec3(rect.maxX, 0, z));
        }

        // Bottom edge (right to left)
        for (int x = rect.maxX - step; x >= rect.minX; x -= step)
        {
            positions.Add(new IntVec3(x, 0, rect.minZ));
        }

        // Left edge (bottom to top)
        for (int z = rect.minZ + step; z <= rect.maxZ; z += step)
        {
            positions.Add(new IntVec3(rect.minX, 0, z));
        }

        // Alternate placement: spawn every other position
        for (int i = 0; i < positions.Count; i += 2)
        {
            IntVec3 pos = positions[i];
            if (pos.InBounds(map) && pos.Standable(map))
            {
                Thing autocannon = ThingMaker.MakeThing(ReptileHunterFactionDefOf.TrapIED_HighExplosive);
                autocannon.SetFaction(rp.faction);
                GenSpawn.Spawn(autocannon, pos, map);
            }
        }
    }

}
