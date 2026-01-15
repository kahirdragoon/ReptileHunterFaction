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

namespace ReptileHunterFaction;

[HarmonyPatch(typeof(Settlement), "get_MapGeneratorDef")]
public static class Patch_Settlement_MapGeneratorDef
{
    [HarmonyPrefix]
    public static bool Prefix(Settlement __instance, ref MapGeneratorDef __result)
    {
        __result = ReptileHunterFactionDefOf.RHF_Faction;
        return false;

        if (__instance?.Faction?.def == ReptileHunterFactionDefOf.RHF_ReptileHunters && ReptileHunterFactionDefOf.RHF_Faction != null)
        {
            __result = ReptileHunterFactionDefOf.RHF_Faction;
            return false; 
        }
        return true; 
    }
}
