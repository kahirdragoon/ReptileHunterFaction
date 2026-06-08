using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PawnHunters;

[HarmonyPatch(typeof(Settlement), "get_MapGeneratorDef")]
public static class Patch_Settlement_MapGeneratorDef
{
    public static bool Prefix(Settlement __instance, ref MapGeneratorDef __result)
    {
        if (__instance?.Faction?.def == PawnHuntersDefOf.PH_PawnHunters && PawnHuntersDefOf.PH_Faction != null)
        {
            __result = PawnHuntersDefOf.PH_Faction;
            return false; 
        }
        return true; 
    }
}
