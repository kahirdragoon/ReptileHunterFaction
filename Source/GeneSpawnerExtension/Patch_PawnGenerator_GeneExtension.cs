using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GeneSpawnerExtension;

[StaticConstructorOnStartup]
internal static class GeneSpawnerExtensionInit
{
    static GeneSpawnerExtensionInit()
    {
        new Harmony("kahirdragoon.GeneSpawnerExtension").PatchAll(Assembly.GetExecutingAssembly());
    }
}

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), [typeof(PawnGenerationRequest)])]
public static class Patch_PawnGenerator_GeneExtension
{
    [HarmonyPostfix]
    public static void AddGenesToPawn(Pawn __result)
    {
        try
        {
            if (__result?.genes == null)
                return;

            var pawnKindExtension = __result.kindDef?.GetModExtension<SpawnGenesExtension>();
            var factionExtension = __result.Faction?.def.GetModExtension<SpawnGenesExtension>();

            if (pawnKindExtension != null)
                ApplyExtension(pawnKindExtension, __result);
            if (factionExtension != null)
                ApplyExtension(factionExtension, __result);
        }
        catch (Exception ex)
        {
            Log.Error($"[GeneSpawnerExtension] Error adding genes to pawn {__result?.Name}. Skipping. Error: {ex}");
        }
    }

    private static void ApplyExtension(SpawnGenesExtension extension, Pawn pawn)
    {
        if (extension.groups == null || extension.groups.Count == 0)
            return;

        var pawnGenes = pawn.genes;
        var groups = extension.randomOrder
            ? extension.groups.InRandomOrder().ToList()
            : extension.groups;

        var groupsApplied = 0;
        foreach (var group in groups)
        {
            if (extension.maxGroupsApplied.HasValue && groupsApplied >= extension.maxGroupsApplied.Value)
                break;

            if (!ConditionsMet(group))
                continue;

            if (!Rand.Chance(group.chance))
                continue;

            ApplyGenes(group, pawnGenes);
            ApplyMetOffsetGenes(group.metOffsetGenes, pawnGenes);

            groupsApplied++;
        }

        ApplyMetOffsetGenes(extension.metOffsetGenes, pawnGenes);
        ApplyXenotypeNameOverrides(extension, pawnGenes);
    }

    private static bool ConditionsMet(GeneGroup group)
    {
        var map = Find.AnyPlayerHomeMap;

        if (group.minRaidPoints.HasValue || group.maxRaidPoints.HasValue)
        {
            var raidPoints = map != null ? StorytellerUtility.DefaultThreatPointsNow(map) : 0f;
            if (group.minRaidPoints.HasValue && raidPoints < group.minRaidPoints.Value)
                return false;
            if (group.maxRaidPoints.HasValue && raidPoints > group.maxRaidPoints.Value)
                return false;
        }

        if (group.minWealth.HasValue || group.maxWealth.HasValue)
        {
            var wealth = map?.wealthWatcher?.WealthTotal ?? 0f;
            if (group.minWealth.HasValue && wealth < group.minWealth.Value)
                return false;
            if (group.maxWealth.HasValue && wealth > group.maxWealth.Value)
                return false;
        }

        if (group.minDay.HasValue || group.maxDay.HasValue)
        {
            var day = Find.TickManager.TicksGame / (float)GenDate.TicksPerDay;
            if (group.minDay.HasValue && day < group.minDay.Value)
                return false;
            if (group.maxDay.HasValue && day > group.maxDay.Value)
                return false;
        }

        return true;
    }

    private static void ApplyGenes(GeneGroup group, Pawn_GeneTracker pawnGenes)
    {
        if (group.genes == null || group.genes.Count == 0)
            return;

        ResolveGeneDefs(group.genes, pawnGenes.pawn);

        var genesToApply = group.randomOrder
            ? group.genes.InRandomOrder().ToList()
            : group.genes;

        var geneCap = group.maxGenesApplied.min >= 0 ? group.maxGenesApplied.RandomInRange : int.MaxValue;
        var genesAdded = 0;
        foreach (var info in genesToApply)
        {
            if (genesAdded >= geneCap)
                break;

            if (info.geneDef == null)
                continue;

            if (pawnGenes.HasActiveGene(info.geneDef))
                continue;

            if (!Rand.Chance(info.chance))
                continue;

            pawnGenes.AddGene(info.geneDef, info.xenogene);
            genesAdded++;
        }
    }

    private static void ApplyMetOffsetGenes(MetOffsetGeneConfig? config, Pawn_GeneTracker pawnGenes)
    {
        if (config?.genes == null || config.genes.Count == 0)
            return;

        var maxMet = GeneTuning.BiostatRange.TrueMax;
        var currentMet = pawnGenes.GenesListForReading.Where(g => g.Active).Sum(g => g.def.biostatMet);

        if (currentMet <= maxMet)
            return;

        ResolveGeneDefs(config.genes, pawnGenes.pawn);

        var offsetGenes = config.randomOrder
            ? config.genes.InRandomOrder().ToList()
            : config.genes;

        foreach (var info in offsetGenes)
        {
            if (currentMet <= maxMet)
                break;

            if (info.geneDef == null || info.geneDef.biostatMet >= 0)
                continue;

            if (pawnGenes.HasActiveGene(info.geneDef))
                continue;

            if (!Rand.Chance(info.chance))
                continue;

            pawnGenes.AddGene(info.geneDef, info.xenogene);
            currentMet += info.geneDef.biostatMet;
        }
    }

    private static void ResolveGeneDefs(List<GeneSpawnInfo> genes, Pawn pawn)
    {
        foreach (var info in genes)
        {
            if (info.geneDef != null || info.defName == null)
                continue;

            info.geneDef = DefDatabase<GeneDef>.GetNamedSilentFail(info.defName);
            if (info.geneDef == null)
                Log.Warning($"[GeneSpawnerExtension] GeneDef '{info.defName}' not found (pawn: {pawn.Name}).");
        }
    }

    private static void ApplyXenotypeNameOverrides(SpawnGenesExtension extension, Pawn_GeneTracker pawnGenes)
    {
        if (extension.xenotypeNameReplacement != null)
        {
            pawnGenes.xenotypeName = extension.xenotypeNameReplacement;
            return;
        }

        if (extension.xenotypeNamePrefix != null)
            pawnGenes.xenotypeName = extension.xenotypeNamePrefix + " " + pawnGenes.xenotypeName;

        if (extension.xenotypeNameSuffix != null)
            pawnGenes.xenotypeName = pawnGenes.xenotypeName + " " + extension.xenotypeNameSuffix;
    }
}
