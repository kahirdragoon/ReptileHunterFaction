using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ReptileHunterFaction;

internal class ReptileHunterFactionMod : Mod
{
    public static RHFModSettings Settings = null!;

    // UI state
    private Vector2 _xenoScroll;
    private Vector2 _geneScroll;
    private string  _geneFilter = "";

    public ReptileHunterFactionMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<RHFModSettings>();
        new Harmony("kahirdragoon.ReptileHunterFaction").PatchAll();
        RHFPawnTargetingUtility.RebuildCache();
    }

    public override string SettingsCategory() => "Reptile Hunter Faction";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        const float LabelHeight  = 28f;
        const float RowHeight    = 24f;
        const float Padding      = 6f;
        const float TopAreaHeight = LabelHeight * 2 + Padding * 3;

        // ── Top controls ───────────────────────────────────────────────
        var listing = new Listing_Standard();
        listing.Begin(inRect.TopPartPixels(TopAreaHeight));

        // Min qualifying pawns
        string minLabel = "RHF_Settings_MinQualifyingPawns".Translate();
        Rect minRow = listing.GetRect(LabelHeight);
        Widgets.Label(minRow.LeftHalf(), minLabel);
        string minStr = Settings.minQualifyingPawns.ToString();
        Widgets.TextFieldNumeric(minRow.RightHalf(), ref Settings.minQualifyingPawns, ref minStr, 0, 99);

        // Gene match mode
        Rect modeRow = listing.GetRect(LabelHeight);
        Widgets.Label(modeRow.LeftHalf(), "RHF_Settings_GeneMatchMode".Translate());
        bool requireAll = Settings.geneMatchRequiresAll;
        Rect rightHalf = modeRow.RightHalf();
        Rect anyRect = rightHalf.LeftHalf();
        Rect allRect = rightHalf.RightHalf();
        allRect.x     += 5f;
        allRect.width -= 5f;
        if (Widgets.RadioButtonLabeled(anyRect, "RHF_Settings_AnyGene".Translate(), !requireAll))
            Settings.geneMatchRequiresAll = false;
        if (Widgets.RadioButtonLabeled(allRect, "RHF_Settings_AllGenes".Translate(), requireAll))
            Settings.geneMatchRequiresAll = true;

        listing.End();

        // ── Two-panel scroll area ───────────────────────────────────────
        Rect panelArea = inRect.BottomPartPixels(inRect.height - TopAreaHeight - Padding);
        Rect leftPanel  = panelArea.LeftHalf().ContractedBy(Padding);
        Rect rightPanel = panelArea.RightHalf().ContractedBy(Padding);

        DrawXenotypePanel(leftPanel, RowHeight);
        DrawGenePanel(rightPanel, RowHeight);

        RHFPawnTargetingUtility.RebuildCache();
    }

    private void DrawXenotypePanel(Rect rect, float rowH)
    {
        var allXeno = DefDatabase<XenotypeDef>.AllDefsListForReading
            .OrderBy(x => x.label ?? x.defName)
            .ToList();

        Widgets.Label(rect.TopPartPixels(22f), "RHF_Settings_TargetXenotypes".Translate());
        Rect scrollRect = rect.BottomPartPixels(rect.height - 24f);
        Rect viewRect   = new(0, 0, scrollRect.width - 16f, allXeno.Count * rowH);

        Widgets.BeginScrollView(scrollRect, ref _xenoScroll, viewRect);
        float y = 0;
        foreach (var xeno in allXeno)
        {
            bool selected = Settings.targetXenotypes.Contains(xeno.defName);
            bool newVal   = selected;
            Widgets.CheckboxLabeled(new Rect(0, y, viewRect.width, rowH),
                xeno.label?.CapitalizeFirst() ?? xeno.defName, ref newVal);
            if (newVal != selected)
            {
                if (newVal) Settings.targetXenotypes.Add(xeno.defName);
                else        Settings.targetXenotypes.Remove(xeno.defName);
            }
            y += rowH;
        }
        Widgets.EndScrollView();
    }

    private void DrawGenePanel(Rect rect, float rowH)
    {
        Widgets.Label(rect.TopPartPixels(22f), "RHF_Settings_TargetGenes".Translate());

        // Search field
        Rect searchRect = new Rect(rect.x, rect.y + 24f, rect.width, 24f);
        _geneFilter = Widgets.TextField(searchRect, _geneFilter);

        Rect scrollRect = new Rect(rect.x, rect.y + 52f, rect.width, rect.height - 52f);

        string filter = _geneFilter.ToLowerInvariant();
        var allGenes = DefDatabase<GeneDef>.AllDefsListForReading
            .Where(g => filter.NullOrEmpty()
                        || (g.label ?? g.defName).ToLowerInvariant().Contains(filter))
            .OrderBy(g => g.label ?? g.defName)
            .ToList();

        Rect viewRect = new Rect(0, 0, scrollRect.width - 16f, allGenes.Count * rowH);

        Widgets.BeginScrollView(scrollRect, ref _geneScroll, viewRect);
        float y = 0;
        foreach (var gene in allGenes)
        {
            bool selected = Settings.targetGenes.Contains(gene.defName);
            bool newVal   = selected;
            Widgets.CheckboxLabeled(new Rect(0, y, viewRect.width, rowH),
                gene.label?.CapitalizeFirst() ?? gene.defName, ref newVal);
            if (newVal != selected)
            {
                if (newVal) Settings.targetGenes.Add(gene.defName);
                else        Settings.targetGenes.Remove(gene.defName);
            }
            y += rowH;
        }
        Widgets.EndScrollView();
    }
}
