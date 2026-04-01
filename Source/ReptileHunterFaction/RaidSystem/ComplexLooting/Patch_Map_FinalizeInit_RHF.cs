using HarmonyLib;
using RimWorld.Planet;
using System.Linq;
using Verse;

namespace ReptileHunterFaction;

[HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
public static class Patch_Map_FinalizeInit_RHF
{
    public static void Postfix(Map __instance)
    {
        if (__instance.components.OfType<MapComponent_RHF_ComplexWatch>().Any()) return;

        if (__instance.Parent is Site site &&
            site.parts.Any(p => p.def.tags?.Contains("AncientComplex") == true) &&
            Find.FactionManager.FirstFactionOfDef(ReptileHunterFactionDefOf.RHF_ReptileHunters) != null)
        {
            var comp = new MapComponent_RHF_ComplexWatch(__instance);
            __instance.components.Add(comp);
        }
    }
}
