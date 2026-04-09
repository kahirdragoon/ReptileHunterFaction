using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace GeneSpawnerExtension;

/// <summary>
/// Full-screen dialog for editing a single GeneGroup — chance, conditions,
/// gene list, and per-group met offset genes.
/// </summary>
public class Dialog_GSEGeneGroup : Window
{
    private readonly GeneGroup _group;

    private string _chanceStr;
    private string _maxGenesMinStr;
    private string _maxGenesMaxStr;
    private string _minRaidPtsStr;
    private string _maxRaidPtsStr;
    private string _minWealthStr;
    private string _maxWealthStr;
    private string _minDayStr;
    private string _maxDayStr;

    private Vector2 _genesScroll;
    private Vector2 _metScroll;

    private const float RowH = 24f;
    private const float LabelW = 120f;
    private const float FieldW = 80f;
    private const float Pad = 6f;

    public override Vector2 InitialSize => new(700f, 700f);

    public Dialog_GSEGeneGroup(GeneGroup group)
    {
        _group = group;
        doCloseButton = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;

        _chanceStr        = group.chance.ToString("F2");
        _maxGenesMinStr   = group.maxGenesApplied.min >= 0 ? group.maxGenesApplied.min.ToString() : "";
        _maxGenesMaxStr   = group.maxGenesApplied.max >= 0 ? group.maxGenesApplied.max.ToString() : "";
        _minRaidPtsStr    = group.minRaidPoints >= 0 ? group.minRaidPoints.ToString("F0") : "";
        _maxRaidPtsStr    = group.maxRaidPoints >= 0 ? group.maxRaidPoints.ToString("F0") : "";
        _minWealthStr     = group.minWealth >= 0 ? group.minWealth.ToString("F0") : "";
        _maxWealthStr     = group.maxWealth >= 0 ? group.maxWealth.ToString("F0") : "";
        _minDayStr        = group.minDay >= 0 ? group.minDay.ToString("F0") : "";
        _maxDayStr        = group.maxDay >= 0 ? group.maxDay.ToString("F0") : "";
    }

    public override void DoWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        // ── Top fields ──────────────────────────────────────────────────
        DrawLabeledFloat(listing, "Chance (0-1):", ref _chanceStr, v => _group.chance = Mathf.Clamp01(v));

        DrawRangePair(listing, "Max Genes (min~max):", ref _maxGenesMinStr, ref _maxGenesMaxStr, (min, max) =>
        {
            _group.maxGenesApplied = new IntRange(
                _maxGenesMinStr.NullOrEmpty() ? -1 : (int)min,
                _maxGenesMaxStr.NullOrEmpty() ? -1 : (int)max);
        });

        bool ro = _group.randomOrder;
        listing.CheckboxLabeled("Random gene order within group", ref ro);
        _group.randomOrder = ro;

        listing.Gap(Pad);
        listing.Label("Conditions (blank = no gate):");

        DrawRangePair(listing, "Raid Points:", ref _minRaidPtsStr, ref _maxRaidPtsStr, (min, max) =>
        {
            _group.minRaidPoints = _minRaidPtsStr.NullOrEmpty() ? -1f : min;
            _group.maxRaidPoints = _maxRaidPtsStr.NullOrEmpty() ? -1f : max;
        });
        DrawRangePair(listing, "Wealth:", ref _minWealthStr, ref _maxWealthStr, (min, max) =>
        {
            _group.minWealth = _minWealthStr.NullOrEmpty() ? -1f : min;
            _group.maxWealth = _maxWealthStr.NullOrEmpty() ? -1f : max;
        });
        DrawRangePair(listing, "Day:", ref _minDayStr, ref _maxDayStr, (min, max) =>
        {
            _group.minDay = _minDayStr.NullOrEmpty() ? -1f : min;
            _group.maxDay = _maxDayStr.NullOrEmpty() ? -1f : max;
        });

        listing.Gap(Pad);
        listing.End();

        // ── Remaining area split: genes top half, met offset bottom half ─
        // Correctly position below listing and above close button
        float usedHeight = listing.CurHeight;
        float remainingTop    = inRect.y + usedHeight + Pad;
        float remainingBottom = inRect.yMax - CloseButSize.y - Pad;
        var remaining = new Rect(inRect.x, remainingTop, inRect.width, remainingBottom - remainingTop);
        float halfH = (remaining.height - Pad) / 2f;

        var genesRect = remaining.TopPartPixels(halfH);
        var metRect   = remaining.BottomPartPixels(halfH);

        DrawGeneSection(genesRect, "Genes", _group.genes ??= [], ref _genesScroll, "Add Gene");
        DrawGeneSection(metRect,   "Met Offset Genes", (_group.metOffsetGenes ??= new MetOffsetGeneConfig()).genes ??= [], ref _metScroll, "Add Met Gene",
            metOffsetOnly: true,
            headerExtra: r =>
            {
                bool mro = _group.metOffsetGenes!.randomOrder;
                // Checkbox placed before the add button (which takes the last 80f)
                var cbRect = new Rect(r.xMax - 210f, r.y, 120f, RowH);
                Widgets.CheckboxLabeled(cbRect, "Random", ref mro, placeCheckboxNearText: true);
                _group.metOffsetGenes.randomOrder = mro;
            });
    }

    private void DrawGeneSection(Rect rect, string label, List<GeneSpawnInfo> genes, ref Vector2 scroll, string addLabel,
        System.Action<Rect>? headerExtra = null, bool metOffsetOnly = false)
    {
        const float HeaderH = 28f;
        const float ChanceColW = 80f;
        const float XenoColW = 70f;
        const float DelColW = 24f;

        // Header row — label uses less width when there's a headerExtra (checkbox before add button)
        var headerRect = rect.TopPartPixels(HeaderH);
        float labelW = headerExtra != null ? headerRect.width - 210f : headerRect.width - 80f;
        Widgets.Label(headerRect.LeftPartPixels(Mathf.Max(labelW, 0f)), label);

        if (headerExtra != null)
            headerExtra(headerRect);

        var addBtn = new Rect(headerRect.xMax - 80f, headerRect.y, 80f, HeaderH);
        if (Widgets.ButtonText(addBtn, addLabel))
            Find.WindowStack.Add(new Dialog_GSEGenePicker(genes, label, metOffsetOnly));

        // Scroll list
        var scrollRect = rect.BottomPartPixels(rect.height - HeaderH - Pad);
        var viewRect   = new Rect(0f, 0f, scrollRect.width - 16f, genes.Count * RowH);

        Widgets.BeginScrollView(scrollRect, ref scroll, viewRect);
        float y = 0f;
        GeneSpawnInfo? toRemove = null;

        foreach (var info in genes)
        {
            var nameRect   = new Rect(0f, y, viewRect.width - ChanceColW - XenoColW - DelColW - Pad * 2, RowH);
            var chanceRect = new Rect(nameRect.xMax + Pad, y, ChanceColW, RowH);
            var xenoRect   = new Rect(chanceRect.xMax + Pad, y, XenoColW, RowH);
            var delRect    = new Rect(xenoRect.xMax + Pad, y, DelColW, RowH);

            // Show resolved gene label if available, fall back to defName
            var displayName = info.geneDef?.label?.CapitalizeFirst()
                ?? (info.defName != null ? DefDatabase<GeneDef>.GetNamedSilentFail(info.defName)?.label?.CapitalizeFirst() : null)
                ?? info.defName ?? "?";
            Widgets.Label(nameRect, displayName);

            var chanceStr = info.chance.ToString("F2");
            Widgets.TextFieldNumeric(chanceRect, ref info.chance, ref chanceStr, 0f, 1f);

            bool xeno = info.xenogene;
            Widgets.CheckboxLabeled(xenoRect, "Xeno", ref xeno, placeCheckboxNearText: true);
            info.xenogene = xeno;

            if (Widgets.ButtonText(delRect, "-"))
                toRemove = info;

            y += RowH;
        }

        if (toRemove != null)
            genes.Remove(toRemove);

        Widgets.EndScrollView();
    }

    private static void DrawLabeledFloat(Listing_Standard listing, string label, ref string buf, System.Action<float> onChanged)
    {
        var row = listing.GetRect(RowH);
        Widgets.Label(row.LeftPartPixels(LabelW), label);
        var field = row.RightPartPixels(FieldW);
        float dummy = 0f;
        var newBuf = buf;
        Widgets.TextFieldNumeric(field, ref dummy, ref newBuf, 0f, 1f);
        if (newBuf != buf)
        {
            buf = newBuf;
            if (float.TryParse(buf, out var v))
                onChanged(v);
        }
    }

    private static void DrawRangePair(Listing_Standard listing, string label, ref string minBuf, ref string maxBuf,
        System.Action<float, float> onChanged)
    {
        var row = listing.GetRect(RowH);
        Widgets.Label(row.LeftPartPixels(LabelW), label);
        var right = row.RightPartPixels(row.width - LabelW - Pad);
        var minRect = right.LeftPartPixels(FieldW);
        Widgets.Label(new Rect(minRect.xMax + 2f, right.y, 20f, RowH), "~");
        var maxRect = new Rect(minRect.xMax + 24f, right.y, FieldW, RowH);

        float dummyMin = 0f, dummyMax = 0f;
        var newMin = minBuf; var newMax = maxBuf;
        Widgets.TextFieldNumeric(minRect, ref dummyMin, ref newMin, 0f, float.MaxValue);
        Widgets.TextFieldNumeric(maxRect, ref dummyMax, ref newMax, 0f, float.MaxValue);

        if (newMin != minBuf || newMax != maxBuf)
        {
            minBuf = newMin; maxBuf = newMax;
            float.TryParse(minBuf, out var minV);
            float.TryParse(maxBuf, out var maxV);
            onChanged(minV, maxV);
        }
    }
}
