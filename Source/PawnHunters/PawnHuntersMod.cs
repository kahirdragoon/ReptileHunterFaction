using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PawnHunters;

internal class PawnHuntersMod : Mod
{
    public static PHModSettings Settings = null!;

    // UI state
    private Vector2 _xenoScroll;
    private Vector2 _geneScroll;
    private string  _geneFilter = "";

    // Sorted once (defs don't change after startup); the gene list is re-filtered only when the search text changes.
    private List<XenotypeDef>? _sortedXenotypes;
    private List<GeneDef>?     _sortedGenes;
    private List<GeneDef>      _filteredGenes = [];
    private string?            _filteredFor;

    public PawnHuntersMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<PHModSettings>();
        new Harmony("kahirdragoon.PawnHunters").PatchAll();
    }

    public override string SettingsCategory() => "Pawn Hunters";

    public override void WriteSettings()
    {
        base.WriteSettings();
        PHPawnTargetingUtility.RebuildCache();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        const float LabelHeight  = 28f;
        const float RowHeight    = 24f;
        const float Padding      = 6f;
        const float TopAreaHeight = LabelHeight * 4 + Padding * 5;

        // ── Top controls ───────────────────────────────────────────────
        var listing = new Listing_Standard();
        listing.Begin(inRect.TopPartPixels(TopAreaHeight));

        // Min qualifying pawns
        string minLabel = "PH_Settings_MinQualifyingPawns".Translate();
        Rect minRow = listing.GetRect(LabelHeight);
        Widgets.Label(minRow.LeftHalf(), minLabel);
        string minStr = Settings.minQualifyingPawns.ToString();
        Widgets.TextFieldNumeric(minRow.RightHalf(), ref Settings.minQualifyingPawns, ref minStr, 0, 99);

        // Min colonists for boss raid
        Rect bossRow = listing.GetRect(LabelHeight);
        Widgets.Label(bossRow.LeftHalf(), "PH_Settings_MinPawnsForBossRaid".Translate());
        string bossStr = Settings.minPawnsForBossRaid.ToString();
        Widgets.TextFieldNumeric(bossRow.RightHalf(), ref Settings.minPawnsForBossRaid, ref bossStr, 1, 99);

        // Min colonists for kidnapping raid
        Rect kidnapRow = listing.GetRect(LabelHeight);
        Widgets.Label(kidnapRow.LeftHalf(), "PH_Settings_MinColonistsForKidnappingRaid".Translate());
        string kidnapStr = Settings.minColonistsForKidnappingRaid.ToString();
        Widgets.TextFieldNumeric(kidnapRow.RightHalf(), ref Settings.minColonistsForKidnappingRaid, ref kidnapStr, 1, 99);

        // Gene match mode
        Rect modeRow = listing.GetRect(LabelHeight);
        Widgets.Label(modeRow.LeftHalf(), "PH_Settings_GeneMatchMode".Translate());
        bool requireAll = Settings.geneMatchRequiresAll;
        Rect rightHalf = modeRow.RightHalf();
        Rect anyRect = rightHalf.LeftHalf();
        Rect allRect = rightHalf.RightHalf();
        allRect.x     += 5f;
        allRect.width -= 5f;
        if (Widgets.RadioButtonLabeled(anyRect, "PH_Settings_AnyGene".Translate(), !requireAll))
            Settings.geneMatchRequiresAll = false;
        if (Widgets.RadioButtonLabeled(allRect, "PH_Settings_AllGenes".Translate(), requireAll))
            Settings.geneMatchRequiresAll = true;

        listing.End();

        // ── Two-panel scroll area ───────────────────────────────────────
        Rect panelArea = inRect.BottomPartPixels(inRect.height - TopAreaHeight - Padding);
        Rect leftPanel  = panelArea.LeftHalf().ContractedBy(Padding);
        Rect rightPanel = panelArea.RightHalf().ContractedBy(Padding);

        DrawXenotypePanel(leftPanel, RowHeight);
        DrawGenePanel(rightPanel, RowHeight);
    }

    private void DrawXenotypePanel(Rect rect, float rowH)
    {
        var allXeno = _sortedXenotypes ??= DefDatabase<XenotypeDef>.AllDefsListForReading
            .OrderBy(x => x.label ?? x.defName)
            .ToList();

        Widgets.Label(rect.TopPartPixels(22f), "PH_Settings_TargetXenotypes".Translate());
        Rect scrollRect = rect.BottomPartPixels(rect.height - 24f);
        Rect viewRect   = new(0, 0, scrollRect.width - 16f, allXeno.Count * rowH);

        Widgets.BeginScrollView(scrollRect, ref _xenoScroll, viewRect);
        GetVisibleRows(_xenoScroll, scrollRect.height, rowH, allXeno.Count, out int first, out int last);
        for (int i = first; i <= last; i++)
        {
            var  xeno     = allXeno[i];
            bool selected = Settings.targetXenotypes.Contains(xeno.defName);
            bool newVal   = selected;
            Widgets.CheckboxLabeled(new Rect(0, i * rowH, viewRect.width, rowH),
                xeno.label?.CapitalizeFirst() ?? xeno.defName, ref newVal);
            if (newVal != selected)
            {
                if (newVal) Settings.targetXenotypes.Add(xeno.defName);
                else        Settings.targetXenotypes.Remove(xeno.defName);
            }
        }
        Widgets.EndScrollView();
    }

    /// <summary>Index range of the rows inside the visible part of a scroll view; only those are drawn.</summary>
    private static void GetVisibleRows(Vector2 scroll, float visibleHeight, float rowH, int count,
        out int first, out int last)
    {
        first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / rowH));
        last  = Mathf.Min(count - 1, Mathf.CeilToInt((scroll.y + visibleHeight) / rowH));
    }

    private void DrawGenePanel(Rect rect, float rowH)
    {
        Widgets.Label(rect.TopPartPixels(22f), "PH_Settings_TargetGenes".Translate());

        // Search field
        Rect searchRect = new Rect(rect.x, rect.y + 24f, rect.width, 24f);
        _geneFilter = Widgets.TextField(searchRect, _geneFilter);

        Rect scrollRect = new Rect(rect.x, rect.y + 52f, rect.width, rect.height - 52f);

        _sortedGenes ??= DefDatabase<GeneDef>.AllDefsListForReading
            .OrderBy(g => g.label ?? g.defName)
            .ToList();
        if (_filteredFor != _geneFilter)
        {
            _filteredFor = _geneFilter;
            string filter = _geneFilter.ToLowerInvariant();
            _filteredGenes = filter.NullOrEmpty()
                ? _sortedGenes
                : _sortedGenes.Where(g => (g.label ?? g.defName).ToLowerInvariant().Contains(filter)).ToList();
        }
        var allGenes = _filteredGenes;

        Rect viewRect = new Rect(0, 0, scrollRect.width - 16f, allGenes.Count * rowH);

        Widgets.BeginScrollView(scrollRect, ref _geneScroll, viewRect);
        GetVisibleRows(_geneScroll, scrollRect.height, rowH, allGenes.Count, out int first, out int last);
        for (int i = first; i <= last; i++)
        {
            var  gene     = allGenes[i];
            bool selected = Settings.targetGenes.Contains(gene.defName);
            bool newVal   = selected;
            Widgets.CheckboxLabeled(new Rect(0, i * rowH, viewRect.width, rowH),
                gene.label?.CapitalizeFirst() ?? gene.defName, ref newVal);
            if (newVal != selected)
            {
                if (newVal) Settings.targetGenes.Add(gene.defName);
                else        Settings.targetGenes.Remove(gene.defName);
            }
        }
        Widgets.EndScrollView();
    }
}
