using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;
public class SymbolResolver_Interior_Druglab : SymbolResolver
{
    public override void Resolve(ResolveParams rp)
    {
        ThingDef drugLabDef = VanillaThingDefOf.DrugLab;
        CellRect room = rp.rect;
        IntVec2 sizeNorthSouth = new(drugLabDef.size.x + 1, drugLabDef.size.z);
        IntVec2 sizeEastWest = new(sizeNorthSouth.z, sizeNorthSouth.x);
        List<(CellRect rect, Rot4 rot)> placements = [];

        if (room.Width >= sizeNorthSouth.x && room.Height >= sizeNorthSouth.z)
        {
            int northZ = room.maxZ - sizeNorthSouth.z + 1;
            for (int x = room.minX; x <= room.maxX - sizeNorthSouth.x + 1; x += sizeNorthSouth.x + 1)
                placements.Add((new CellRect(x, northZ, sizeNorthSouth.x, sizeNorthSouth.z), Rot4.North));

            int southZ = room.minZ;
            for (int x = room.minX; x <= room.maxX - sizeNorthSouth.x + 1; x += sizeNorthSouth.x + 1)
                placements.Add((new CellRect(x, southZ, sizeNorthSouth.x, sizeNorthSouth.z), Rot4.South));
        }

        if (room.Width >= sizeEastWest.x && room.Height >= sizeEastWest.z)
        {
            int eastX = room.maxX - sizeEastWest.x + 1;
            for (int z = room.minZ; z <= room.maxZ - sizeEastWest.z + 1; z += sizeEastWest.z + 1)
                placements.Add((new CellRect(eastX, z, sizeEastWest.x, sizeEastWest.z), Rot4.East));

            int westX = room.minX;
            for (int z = room.minZ; z <= room.maxZ - sizeEastWest.z + 1; z += sizeEastWest.z + 1)
                placements.Add((new CellRect(westX, z, sizeEastWest.x, sizeEastWest.z), Rot4.West));
        }

        HashSet<IntVec3> usedCells = [];
        foreach (var (rect, rot) in placements)
        {
            bool overlaps = false;
            foreach (IntVec3 cell in rect.Cells)
            {
                if (!room.Contains(cell) || usedCells.Contains(cell))
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
                continue;

            foreach (IntVec3 cell in rect.Cells)
                usedCells.Add(cell);

            ResolveParams rpThing = rp with
            {
                rect = rect,
                singleThingDef = drugLabDef,
                thingRot = rot
            };
            BaseGen.symbolStack.Push("thing", rpThing);
        }
        var ingredientDefs = new List<ThingDef>() {
            ThingDefOf.Luciferium,
            ReptileHunterFactionDefOf.RHF_Plant_DrugMedicine,
            ReptileHunterFactionDefOf.RHF_ConcentratedInsectoidBlood,
            VanillaThingDefOf.Neutroamine
        };
        foreach (var ingredientDef in ingredientDefs)
        {
            for (int i = 0; i > Rand.Range(1, 5); i++)
            {
                var cellRect = CellRect.FromCell(rp.rect.Cells.Where(c => !usedCells.Contains(c)).RandomElement());
                usedCells.Add(cellRect.Cells.First());
                var rpIngredients = rp with
                {
                    singleThingDef = ingredientDef,
                    rect = cellRect,
                    singleThingStackCount = Rand.Range(2, 8)
                };
                BaseGen.symbolStack.Push("thing", rpIngredients);
            }
        }
    }
}
