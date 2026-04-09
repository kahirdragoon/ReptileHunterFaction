using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace GeneSpawnerExtension;

/// <summary>
/// Small dialog for adding genes to a group's gene list or met offset list.
/// Displays a searchable, scrollable list of all loaded GeneDefs.
/// </summary>
public class Dialog_GSEGenePicker : Window
{
    private readonly List<GeneSpawnInfo> _target;
    private readonly string _title;
    private readonly bool _metOffsetOnly;
    private string _filter = "";
    private Vector2 _scroll;

    private const float RowHeight = 24f;
    private const float SearchHeight = 28f;
    private const float Padding = 6f;

    public override Vector2 InitialSize => new(480f, 600f);

    /// <param name="metOffsetOnly">When true, only shows genes with negative biostatMet (valid met-offset candidates).</param>
    public Dialog_GSEGenePicker(List<GeneSpawnInfo> target, string title, bool metOffsetOnly = false)
    {
        _target = target;
        _title = title;
        _metOffsetOnly = metOffsetOnly;
        doCloseButton = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(inRect.TopPartPixels(32f), _title);
        Text.Font = GameFont.Small;

        var searchRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, SearchHeight);
        _filter = Widgets.TextField(searchRect, _filter);

        var scrollArea = new Rect(inRect.x, searchRect.yMax + Padding, inRect.width, inRect.height - searchRect.yMax - Padding - CloseButSize.y - Padding);

        var filter = _filter.ToLowerInvariant();
        var allGenes = DefDatabase<GeneDef>.AllDefsListForReading
            .Where(g => (!_metOffsetOnly || g.biostatMet < 0)
                        && (filter.NullOrEmpty()
                            || (g.label ?? g.defName).ToLowerInvariant().Contains(filter)
                            || g.defName.ToLowerInvariant().Contains(filter)))
            .OrderBy(g => g.label ?? g.defName)
            .ToList();

        var viewRect = new Rect(0f, 0f, scrollArea.width - 16f, allGenes.Count * RowHeight);
        Widgets.BeginScrollView(scrollArea, ref _scroll, viewRect);

        float y = 0f;
        foreach (var gene in allGenes)
        {
            var existing = _target.FirstOrDefault(i => i.defName == gene.defName || i.geneDef == gene);
            bool selected = existing != null;
            bool newVal = selected;

            var row = new Rect(0f, y, viewRect.width, RowHeight);
            Widgets.CheckboxLabeled(row, gene.label?.CapitalizeFirst() ?? gene.defName, ref newVal);

            if (newVal != selected)
            {
                if (newVal)
                    _target.Add(new GeneSpawnInfo { defName = gene.defName, geneDef = gene });
                else if (existing != null)
                    _target.Remove(existing);
            }

            y += RowHeight;
        }

        Widgets.EndScrollView();
    }
}
