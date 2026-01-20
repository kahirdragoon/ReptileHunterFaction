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

[HarmonyPatch(typeof(GlobalSettings), nameof(GlobalSettings.Clear))]
public static class Patch_GlobalSetting_Clear
{
    public static void Postfix()
    {
        BaseGen_RHFGlobalSettings.Clear();
    }
}
