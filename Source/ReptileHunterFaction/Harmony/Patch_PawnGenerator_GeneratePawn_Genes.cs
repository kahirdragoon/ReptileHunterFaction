using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Analytics;
using Verse;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace ReptileHunterFaction;
[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new Type[] {typeof(PawnGenerationRequest)})]
public static class Patch_PawnGenerator_GeneratePawn_Genes
{
    //private static Stopwatch stopwatch = new Stopwatch();

    [HarmonyPostfix]
    public static void AddGenesToPawn(Pawn __result)
    {
        //stopwatch.Restart();
        try
        {
            if (__result?.genes == null)
                return;

            var pawnKindExtension = __result.kindDef?.GetModExtension<SpawnGenesExtension>();
            var factionExtension = __result.Faction?.def.GetModExtension<SpawnGenesExtension>();

            if (pawnKindExtension != null)
                AddGenes(pawnKindExtension, __result.genes);
            if (factionExtension != null)
                AddGenes(factionExtension, __result.genes);
        }
        catch (Exception ex)
        {
            Log.Error($"[ReptileHunterFaction] Error while trying to add genes to pawn {__result.Name}. Skipping. Error: {ex}");
        }
        //Log.Warning($"[ReptileHunterFaction] Gene addition took {stopwatch.ElapsedMilliseconds} ms for pawn {__result.Name}");
    }

    private static void AddGenes(SpawnGenesExtension extension, Pawn_GeneTracker pawnGenes)
    {
        if(extension.genes == null || extension.genes.Count == 0)
            return;

        var geneSpawnInfos = extension.randomOrder ? extension.genes.InRandomOrder() : extension.genes;

        var validGeneDefs = new List<GeneSpawnInfo>();
        foreach (var geneSpawnInfo in geneSpawnInfos)
        {
            geneSpawnInfo.geneDef = DefDatabase<GeneDef>.GetNamedSilentFail(geneSpawnInfo.defName);
            if (geneSpawnInfo.geneDef != null)
                validGeneDefs.Add(geneSpawnInfo);
            else
                Log.Warning($"GeneDef '{geneSpawnInfo.defName}' not found for pawn '{pawnGenes.pawn.Name}'.");
        }

        foreach (var geneSpawnInfo in validGeneDefs)
            AddGene(geneSpawnInfo, pawnGenes);
        
        if(extension.respectMetabolicEfficiency)
            AdjustForMetabolicEfficiency(pawnGenes, validGeneDefs);
    }

    private static void AddGene(GeneSpawnInfo geneSpawnInfo, Pawn_GeneTracker pawnGenes)
    {
        if (!pawnGenes.HasActiveGene(geneSpawnInfo.geneDef) && Rand.Chance(geneSpawnInfo.chance))
            pawnGenes.AddGene(geneSpawnInfo.geneDef, geneSpawnInfo.xenogene);
    }

    private static void AdjustForMetabolicEfficiency(Pawn_GeneTracker pawnGenes, List<GeneSpawnInfo> geneSpawnInfos)
    {
        var minMet = GeneTuning.BiostatRange.TrueMin;
        var maxMet = GeneTuning.BiostatRange.TrueMax;
        var plusMetGenes = geneSpawnInfos.Select(g => g.geneDef).OfType<GeneDef>().Where(g => g.biostatMet >= 0).ToList();
        var minusMetGenes = geneSpawnInfos.Select(g => g.geneDef).OfType<GeneDef>().Where(g => g.biostatMet < 0).ToList();
        var met = pawnGenes.GenesListForReading.Where(g => g.Active).Select(g => g.def.biostatMet).Sum();

        while (met > maxMet && plusMetGenes.Any())
            RemoveLastGene(plusMetGenes, pawnGenes, ref met);

        while (met < minMet && minusMetGenes.Any())
            RemoveLastGene(minusMetGenes, pawnGenes, ref met);
    }

    private static void RemoveLastGene(List<GeneDef> geneList, Pawn_GeneTracker pawnGenes, ref int met)
    {
        var geneDefToRemove = geneList.Last();
        var geneToRemove = pawnGenes.GetGene(geneDefToRemove);
        pawnGenes.RemoveGene(geneToRemove);
        met = pawnGenes.GenesListForReading.Where(g => g.Active).Select(g => g.def.biostatMet).Sum();
        geneList.RemoveAt(geneList.Count - 1);
    }
}
