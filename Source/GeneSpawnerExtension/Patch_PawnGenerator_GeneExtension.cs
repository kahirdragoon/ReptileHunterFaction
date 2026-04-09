using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GeneSpawnerExtension;

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

            var factionExtension = __result.Faction?.def.GetModExtension<SpawnGenesExtension>();
            var pawnKindExtension = __result.kindDef?.GetModExtension<SpawnGenesExtension>();

            if (factionExtension != null)
                ApplyExtension(factionExtension, __result);
            if (pawnKindExtension != null)
                ApplyExtension(pawnKindExtension, __result);

            // Player-configured settings (applied after XML extensions)
            if (GeneSpawnerExtensionMod.Settings != null)
            {
                var factionConfig = GeneSpawnerExtensionMod.Settings.GetFactionConfig(__result.Faction?.def?.defName);
                var pawnKindConfig = GeneSpawnerExtensionMod.Settings.GetPawnKindConfig(__result.kindDef?.defName);

                if (factionConfig != null)
                    ApplyConfig(factionConfig, __result);
                if (pawnKindConfig != null)
                    ApplyConfig(pawnKindConfig, __result);
            }
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
            ? [.. extension.groups.InRandomOrder()]
            : extension.groups;

        var groupsApplied = 0;
        foreach (var group in groups)
        {
            if (extension.maxGroupsApplied >= 0 && groupsApplied >= extension.maxGroupsApplied)
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
        ApplyXenotypeNameOverrides(extension.xenotypeNamePrefix, extension.xenotypeNameSuffix, extension.xenotypeNameReplacement, pawnGenes);
    }

    private static void ApplyConfig(DefGeneSpawnConfig config, Pawn pawn)
    {
        if (config.groups == null || config.groups.Count == 0)
            return;

        var pawnGenes = pawn.genes;
        var groups = config.randomOrder
            ? config.groups.InRandomOrder().ToList()
            : config.groups;

        var groupsApplied = 0;
        foreach (var group in groups)
        {
            if (config.maxGroupsApplied >= 0 && groupsApplied >= config.maxGroupsApplied)
                break;

            if (!ConditionsMet(group))
                continue;

            if (!Rand.Chance(group.chance))
                continue;

            ApplyGenes(group, pawnGenes);
            ApplyMetOffsetGenes(group.metOffsetGenes, pawnGenes);

            groupsApplied++;
        }

        ApplyMetOffsetGenes(config.metOffsetGenes, pawnGenes);
        ApplyXenotypeNameOverrides(config.xenotypeNamePrefix, config.xenotypeNameSuffix, config.xenotypeNameReplacement, pawnGenes);
    }

    private static bool ConditionsMet(GeneGroup group)
    {
        var map = Find.AnyPlayerHomeMap;

        if (group.minRaidPoints >= 0 || group.maxRaidPoints >= 0)
        {
            var raidPoints = map != null ? StorytellerUtility.DefaultThreatPointsNow(map) : 0f;
            if (group.minRaidPoints >= 0 && raidPoints < group.minRaidPoints)
                return false;
            if (group.maxRaidPoints >= 0 && raidPoints > group.maxRaidPoints)
                return false;
        }

        if (group.minWealth >= 0 || group.maxWealth >= 0)
        {
            var wealth = map?.wealthWatcher?.WealthTotal ?? 0f;
            if (group.minWealth >= 0 && wealth < group.minWealth)
                return false;
            if (group.maxWealth >= 0 && wealth > group.maxWealth)
                return false;
        }

        if (group.minDay >= 0 || group.maxDay >= 0)
        {
            var day = Find.TickManager.TicksGame / (float)GenDate.TicksPerDay;
            if (group.minDay >= 0 && day < group.minDay)
                return false;
            if (group.maxDay >= 0 && day > group.maxDay)
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
            ? [.. config.genes.InRandomOrder()]
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

    private static void ApplyXenotypeNameOverrides(string? prefix, string? suffix, string? replacement, Pawn_GeneTracker pawnGenes)
    {
        if (replacement != null)
        {
            pawnGenes.xenotypeName = replacement;
            return;
        }

        if (prefix != null)
            pawnGenes.xenotypeName = prefix + " " + pawnGenes.xenotypeName;

        if (suffix != null)
            pawnGenes.xenotypeName = pawnGenes.xenotypeName + " " + suffix;
    }
}
