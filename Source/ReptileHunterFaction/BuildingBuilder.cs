using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace ReptileHunterFaction;

internal static class BuildingBuilder
{
    public static CellRect CreateBuilding(IntVec3 centerCell, IntRange size)
    {
        var prisonRect = CellRect.CenteredOn(centerCell, size.RandomInRange, size.RandomInRange);
        //prisonRect.ClipInsideMap(BaseGen.globalSettings.map);
        //var doorCell = prisonRect.RandomCell;
        //var door = ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.Plasteel);
        //GenSpawn.Spawn(door, doorCell, BaseGen.globalSettings.map);
        foreach(var cell in prisonRect.Cells)
        {
            
        }
        foreach (var cell in prisonRect.EdgeCells)
        {
            if (cell.InBounds(BaseGen.globalSettings.map))
            {
                var thing = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Plasteel);
                GenSpawn.Spawn(thing, cell, BaseGen.globalSettings.map);
            }
        }
        foreach (var cell in prisonRect)
        {
            if (cell.InBounds(BaseGen.globalSettings.map))
                BaseGen.globalSettings.map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
        }
        return prisonRect;
    }
}
