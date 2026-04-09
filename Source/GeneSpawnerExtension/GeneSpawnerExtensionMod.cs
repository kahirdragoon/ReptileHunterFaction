using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GeneSpawnerExtension;

public class GeneSpawnerExtensionMod : Mod
{
    public static GeneSpawnerSettings Settings = null!;

    // UI state
    private bool _showFactions = true;       // false = pawnkinds
    private string _defFilter = "";
    private string? _selectedDefName;        // currently selected def (faction or pawnkind)
    private Vector2 _defListScroll;
    private Vector2 _groupListScroll;
    private Vector2 _metOffsetScroll;

    private const float Pad = 6f;
    private const float RowH = 24f;
    private const float TabH = 30f;
    private const float DefListW = 220f;

    public GeneSpawnerExtensionMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<GeneSpawnerSettings>();
        new Harmony("kahirdragoon.GeneSpawnerExtension").PatchAll(Assembly.GetExecutingAssembly());
    }

    public override string SettingsCategory() => "Gene Spawner";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        // Split into left (def list) and right (config editor)
        var leftRect  = new Rect(inRect.x, inRect.y, DefListW, inRect.height);
        var rightRect = new Rect(inRect.x + DefListW + Pad, inRect.y, inRect.width - DefListW - Pad, inRect.height);

        DrawDefList(leftRect);
        DrawConfigEditor(rightRect);
    }

    // ── Left panel ───────────────────────────────────────────────────────

    private void DrawDefList(Rect rect)
    {
        // Tab bar — highlight selected tab manually; ButtonText with active:true suppresses clicks
        var tabRow = rect.TopPartPixels(TabH);
        var factionTab  = tabRow.LeftHalf();
        var pawnKindTab = tabRow.RightHalf();
        if (_showFactions)
            Widgets.DrawBoxSolid(factionTab, new Color(0.3f, 0.3f, 0.3f));
        else
            Widgets.DrawBoxSolid(pawnKindTab, new Color(0.3f, 0.3f, 0.3f));

        if (Widgets.ButtonText(factionTab, "Factions") && !_showFactions)
        {
            _showFactions = true;
            _selectedDefName = null;
            _defFilter = "";
        }
        if (Widgets.ButtonText(pawnKindTab, "Pawn Kinds") && _showFactions)
        {
            _showFactions = false;
            _selectedDefName = null;
            _defFilter = "";
        }

        // Search
        var searchRect = new Rect(rect.x, rect.y + TabH + Pad, rect.width, RowH);
        _defFilter = Widgets.TextField(searchRect, _defFilter);

        // Scrollable list
        var scrollArea = new Rect(rect.x, searchRect.yMax + Pad, rect.width, rect.height - TabH - RowH - Pad * 3);

        var items = GetFilteredDefs();
        var viewRect = new Rect(0f, 0f, scrollArea.width - 16f, items.Count * RowH);

        Widgets.BeginScrollView(scrollArea, ref _defListScroll, viewRect);
        float y = 0f;
        var configs = _showFactions ? Settings.factionConfigs : Settings.pawnKindConfigs;

        foreach (var (defName, label) in items)
        {
            bool hasConfig = configs.Any(c => c.defName == defName);
            bool selected  = _selectedDefName == defName;

            var row = new Rect(0f, y, viewRect.width - 28f, RowH);
            var btn = new Rect(row.xMax + 2f, y, 26f, RowH);

            // Highlight selected
            if (selected)
                Widgets.DrawHighlightSelected(row);
            else if (Mouse.IsOver(row))
                Widgets.DrawHighlight(row);

            // Bold if configured
            Text.Font = hasConfig ? GameFont.Small : GameFont.Tiny;
            if (Widgets.ButtonInvisible(row))
                _selectedDefName = defName;
            Widgets.Label(row, label);
            Text.Font = GameFont.Small;

            // [+] / [×] button
            if (hasConfig)
            {
                if (Widgets.ButtonText(btn, "-"))
                    RemoveConfig(defName);
            }
            else
            {
                if (Widgets.ButtonText(btn, "+"))
                {
                    AddConfig(defName);
                    _selectedDefName = defName;
                }
            }

            y += RowH;
        }

        Widgets.EndScrollView();
    }

    private List<(string defName, string label)> GetFilteredDefs()
    {
        var filter = _defFilter.ToLowerInvariant();

        if (_showFactions)
        {
            return DefDatabase<FactionDef>.AllDefsListForReading
                .Where(d => filter.NullOrEmpty()
                            || (d.label ?? d.defName).ToLowerInvariant().Contains(filter)
                            || d.defName.ToLowerInvariant().Contains(filter))
                .OrderBy(d => d.label ?? d.defName)
                .Select(d => (d.defName, d.label?.CapitalizeFirst() ?? d.defName))
                .ToList();
        }
        else
        {
            return DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(d => filter.NullOrEmpty()
                            || (d.label ?? d.defName).ToLowerInvariant().Contains(filter)
                            || d.defName.ToLowerInvariant().Contains(filter))
                .OrderBy(d => d.label ?? d.defName)
                .Select(d => (d.defName, d.label?.CapitalizeFirst() ?? d.defName))
                .ToList();
        }
    }

    private void AddConfig(string defName)
    {
        var list = _showFactions ? Settings.factionConfigs : Settings.pawnKindConfigs;
        if (list.All(c => c.defName != defName))
            list.Add(new DefGeneSpawnConfig { defName = defName });
        Settings.InvalidateCache();
    }

    private void RemoveConfig(string defName)
    {
        var list = _showFactions ? Settings.factionConfigs : Settings.pawnKindConfigs;
        list.RemoveAll(c => c.defName == defName);
        Settings.InvalidateCache();
        if (_selectedDefName == defName)
            _selectedDefName = null;
    }

    // ── Right panel ──────────────────────────────────────────────────────

    private void DrawConfigEditor(Rect rect)
    {
        if (_selectedDefName == null)
        {
            Widgets.Label(rect, "Select a faction or pawn kind on the left.");
            return;
        }

        var list = _showFactions ? Settings.factionConfigs : Settings.pawnKindConfigs;
        var config = list.FirstOrDefault(c => c.defName == _selectedDefName);
        if (config == null)
        {
            Widgets.Label(rect, "No config. Click [+] to create one.");
            return;
        }

        var listing = new Listing_Standard();
        listing.Begin(rect.TopPartPixels(200f));

        // Top-level settings
        listing.Label($"Config: {_selectedDefName}");
        listing.Gap(Pad);

        // randomOrder
        bool ro = config.randomOrder;
        listing.CheckboxLabeled("Random group order", ref ro);
        config.randomOrder = ro;

        // maxGroupsApplied
        var maxGrpRow = listing.GetRect(RowH);
        Widgets.Label(maxGrpRow.LeftPartPixels(160f), "Max groups (-1 = all):");
        string mgStr = config.maxGroupsApplied.ToString();
        Widgets.TextFieldNumeric(maxGrpRow.RightPartPixels(60f), ref config.maxGroupsApplied, ref mgStr, -1, 999);

        listing.Gap(Pad);

        // Xenotype name overrides
        listing.Label("Xenotype name overrides:");
        DrawNamedTextField(listing, "Prefix:", ref config.xenotypeNamePrefix);
        DrawNamedTextField(listing, "Suffix:", ref config.xenotypeNameSuffix);
        DrawNamedTextField(listing, "Replacement:", ref config.xenotypeNameReplacement);

        listing.End();

        // ── Groups list ──────────────────────────────────────────────────
        float topUsed  = 200f + Pad;
        float removeH  = RowH + Pad * 2;   // reserve space for remove button at bottom
        float metH     = 120f;
        float bottomH  = metH + RowH + Pad * 2;
        float groupsH  = rect.height - topUsed - bottomH - removeH;

        var groupsArea = new Rect(rect.x, rect.y + topUsed, rect.width, groupsH);
        DrawGroupsList(groupsArea, config);

        // ── Global met offset genes ──────────────────────────────────────
        var metArea = new Rect(rect.x, groupsArea.yMax + Pad, rect.width, bottomH);
        DrawGlobalMetOffset(metArea, config);

        // ── Remove config button ─────────────────────────────────────────
        var removeBtn = new Rect(rect.xMax - 130f, metArea.yMax + Pad, 130f, RowH);
        if (Widgets.ButtonText(removeBtn, "Remove Config"))
            RemoveConfig(_selectedDefName);
    }

    private void DrawGroupsList(Rect rect, DefGeneSpawnConfig config)
    {
        // Header
        var header = rect.TopPartPixels(RowH);
        Widgets.Label(header.LeftPartPixels(header.width - 90f), "Groups:");
        if (Widgets.ButtonText(new Rect(header.xMax - 90f, header.y, 90f, RowH), "+ Add Group"))
        {
            config.groups.Add(new GeneGroup());
            Settings.InvalidateCache();
        }

        // Scroll
        var scrollArea = rect.BottomPartPixels(rect.height - RowH - Pad);
        var viewRect   = new Rect(0f, 0f, scrollArea.width - 16f, config.groups.Count * (RowH + 2f));

        Widgets.BeginScrollView(scrollArea, ref _groupListScroll, viewRect);
        float y = 0f;
        int toRemove = -1;
        int moveUp   = -1;
        int moveDown = -1;

        for (int i = 0; i < config.groups.Count; i++)
        {
            var group = config.groups[i];
            int geneCount = group.genes?.Count ?? 0;
            var row = new Rect(0f, y, viewRect.width, RowH);

            Widgets.DrawBoxSolid(row, i % 2 == 0 ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.12f, 0.12f, 0.12f));

            float x = 4f;
            Widgets.Label(new Rect(x, y, 200f, RowH), $"Group {i + 1}  chance:{group.chance:F2}  genes:{geneCount}");
            x = row.xMax - 26f;

            if (Widgets.ButtonText(new Rect(x, y, 24f, RowH), "-")) toRemove = i;
            x -= 28f;
            if (i < config.groups.Count - 1 && Widgets.ButtonText(new Rect(x, y, 24f, RowH), "↓")) moveDown = i;
            x -= 28f;
            if (i > 0 && Widgets.ButtonText(new Rect(x, y, 24f, RowH), "↑")) moveUp = i;
            x -= 60f;
            if (Widgets.ButtonText(new Rect(x, y, 56f, RowH), "Edit"))
                Find.WindowStack.Add(new Dialog_GSEGeneGroup(group));

            y += RowH + 2f;
        }

        Widgets.EndScrollView();

        if (toRemove >= 0) { config.groups.RemoveAt(toRemove); Settings.InvalidateCache(); }
        if (moveUp   >= 0) { (config.groups[moveUp], config.groups[moveUp - 1]) = (config.groups[moveUp - 1], config.groups[moveUp]); }
        if (moveDown >= 0) { (config.groups[moveDown], config.groups[moveDown + 1]) = (config.groups[moveDown + 1], config.groups[moveDown]); }
    }

    private void DrawGlobalMetOffset(Rect rect, DefGeneSpawnConfig config)
    {
        config.metOffsetGenes ??= new MetOffsetGeneConfig();
        var met = config.metOffsetGenes;

        var headerRow = rect.TopPartPixels(RowH);
        Widgets.Label(headerRow.LeftPartPixels(180f), "Global Met Offset Genes:");

        bool mro = met.randomOrder;
        var cb = new Rect(headerRow.x + 184f, headerRow.y, 80f, RowH);
        Widgets.CheckboxLabeled(cb, "Random", ref mro, placeCheckboxNearText: true);
        met.randomOrder = mro;

        if (Widgets.ButtonText(new Rect(headerRow.xMax - 100f, headerRow.y, 100f, RowH), "+ Add Gene"))
            Find.WindowStack.Add(new Dialog_GSEGenePicker(met.genes ??= [], "Met Offset Genes", metOffsetOnly: true));

        // Compact tag-style display of current genes
        var displayRect = rect.BottomPartPixels(rect.height - RowH - Pad);
        var viewRect    = new Rect(0f, 0f, displayRect.width - 16f, ((met.genes?.Count ?? 0) * RowH));
        Widgets.BeginScrollView(displayRect, ref _metOffsetScroll, viewRect);

        float y = 0f;
        GeneSpawnInfo? toRemove = null;
        foreach (var info in met.genes ?? [])
        {
            var row = new Rect(0f, y, viewRect.width, RowH);
            var geneLabel = info.geneDef?.label?.CapitalizeFirst()
                ?? (info.defName != null ? DefDatabase<GeneDef>.GetNamedSilentFail(info.defName)?.label?.CapitalizeFirst() : null)
                ?? info.defName ?? "?";
            Widgets.Label(new Rect(0f, y, row.width - 28f, RowH), geneLabel);
            if (Widgets.ButtonText(new Rect(row.xMax - 26f, y, 24f, RowH), "-"))
                toRemove = info;
            y += RowH;
        }
        if (toRemove != null) met.genes!.Remove(toRemove);

        Widgets.EndScrollView();
    }

    private static void DrawNamedTextField(Listing_Standard listing, string label, ref string? value)
    {
        var row = listing.GetRect(RowH);
        Widgets.Label(row.LeftPartPixels(100f), label);
        var tmp = value ?? "";
        tmp = Widgets.TextField(row.RightPartPixels(row.width - 104f), tmp);
        value = tmp.NullOrEmpty() ? null : tmp;
    }
}
