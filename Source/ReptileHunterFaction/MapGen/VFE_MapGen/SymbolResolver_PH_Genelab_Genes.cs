using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace ReptileHunterFaction;

public class SymbolResolver_PH_Genelab_Genes : SymbolResolver
{
    private static readonly FieldInfo GeneSetField =
        typeof(GeneSetHolderBase).GetField("geneSet", BindingFlags.NonPublic | BindingFlags.Instance)!;

    public override void Resolve(ResolveParams rp)
    {
        if (!ModLister.BiotechInstalled) return;

        var settings = ReptileHunterFactionMod.Settings;
        bool hasXenotypes = settings.targetXenotypes?.Count > 0;
        bool hasGenes = settings.targetGenes?.Count > 0;

        if (!hasXenotypes && !hasGenes) return;

        Map map = BaseGen.globalSettings.map;
        if (map == null) return;

        // Ensure room data is up to date before querying
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

        // Find all gene banks placed within the settlement rect
        var allGeneBanks = map.listerThings.GetThingsOfType<Building>()
            .Where(b => b.def == ThingDefOf.GeneBank && rp.rect.Contains(b.Position))
            .ToList();

        if (allGeneBanks.Count == 0) return;

        // Group gene banks by room; fall back to a single group if rooms are unavailable
        var banksByRoom = allGeneBanks
            .GroupBy(b => b.GetRoom())
            .Where(g => g.Key != null)
            .Select(g => (room: g.Key!, banks: g.ToList()))
            .ToList();

        if (banksByRoom.Count == 0)
            banksByRoom = [(room: null!, banks: allGeneBanks)];

        // Resolve xenotypes and individual genes from settings
        List<XenotypeDef>? xenotypes = null;
        if (hasXenotypes)
        {
            xenotypes = settings.targetXenotypes
                .Select(n => DefDatabase<XenotypeDef>.GetNamedSilentFail(n))
                .Where(x => x != null)
                .InRandomOrder()
                .ToList()!;
        }

        List<GeneDef>? individualGenes = null;
        if (hasGenes)
        {
            individualGenes = settings.targetGenes
                .Select(n => DefDatabase<GeneDef>.GetNamedSilentFail(n))
                .Where(g => g != null)
                .ToList()!;
        }

        bool hasIndividualGenes = individualGenes is { Count: > 0 };
        bool hasResolvedXenotypes = xenotypes is { Count: > 0 };

        // Assign rooms:
        //   - First room gets all individual genes (if any)
        //   - Remaining rooms each get one xenotype, cycling if there are more rooms than xenotypes
        int roomIdx = 0;

        if (hasIndividualGenes && banksByRoom.Count > 0)
        {
            var (room, banks) = banksByRoom[roomIdx];
            FillBanksWithGenes(banks, individualGenes!);
            if (room != null)
                ReplaceXenogermsOnShelves(room, map, rp.rect, individualGenes!);
            roomIdx++;
        }

        if (hasResolvedXenotypes)
        {
            int xenoIdx = 0;
            for (; roomIdx < banksByRoom.Count; roomIdx++, xenoIdx++)
            {
                var xenotype = xenotypes![xenoIdx % xenotypes.Count];
                var (room, banks) = banksByRoom[roomIdx];
                FillBanksWithXenotype(banks, xenotype);
                if (room != null)
                    ReplaceXenogermsOnShelves(room, map, rp.rect, xenotype);
            }
        }
    }

    /// <summary>Fills gene banks with one genepack per gene from the xenotype until banks are full.</summary>
    private static void FillBanksWithXenotype(List<Building> banks, XenotypeDef xenotype)
    {
        var genes = xenotype.genes;
        if (genes == null || genes.Count == 0) return;

        int geneIdx = 0;
        foreach (var bank in banks)
        {
            var comp = bank.TryGetComp<CompGenepackContainer>();
            if (comp == null) continue;

            while (!comp.Full && geneIdx < genes.Count)
            {
                AddGenepack(comp, [genes[geneIdx]]);
                geneIdx++;
            }
        }
    }

    /// <summary>Shuffles the genes and distributes them one-per-genepack through the banks.</summary>
    private static void FillBanksWithGenes(List<Building> banks, List<GeneDef> genes)
    {
        var shuffled = genes.InRandomOrder().ToList();
        int geneIdx = 0;

        foreach (var bank in banks)
        {
            var comp = bank.TryGetComp<CompGenepackContainer>();
            if (comp == null) continue;

            while (!comp.Full && geneIdx < shuffled.Count)
            {
                AddGenepack(comp, [shuffled[geneIdx]]);
                geneIdx++;
            }
        }
    }

    private static void AddGenepack(CompGenepackContainer comp, List<GeneDef> genes)
    {
        var genepack = (Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack);
        genepack.Initialize(genes);
        comp.innerContainer.TryAdd(genepack);
    }

    /// <summary>Clears shelves in the room and places new xenogerms representing a full xenotype.</summary>
    private static void ReplaceXenogermsOnShelves(Room room, Map map, CellRect rect, XenotypeDef xenotype)
    {
        var shelves = map.listerThings.GetThingsOfType<Building_Storage>()
            .Where(s => rect.Contains(s.Position) && s.GetRoom() == room)
            .ToList();
        PopulateShelves(shelves, xenotype.genes, xenotype.label, XenotypeIconDefOf.Basic, map);
    }

    /// <summary>Clears shelves in the room and places new xenogerms representing individually selected genes.</summary>
    private static void ReplaceXenogermsOnShelves(Room room, Map map, CellRect rect, List<GeneDef> genes)
    {
        var shelves = map.listerThings.GetThingsOfType<Building_Storage>()
            .Where(s => rect.Contains(s.Position) && s.GetRoom() == room)
            .ToList();
        PopulateShelves(shelves, genes, "RHF_SelectedGenes".Translate(), XenotypeIconDefOf.Basic, map);
    }

    private static void PopulateShelves(List<Building_Storage> shelves, List<GeneDef> genes, string label, XenotypeIconDef icon, Map map)
    {
        foreach (var shelf in shelves)
        {
            // Clear all non-building things from every cell of this shelf
            foreach (var cell in shelf.OccupiedRect())
            {
                var things = cell.GetThingList(map).Where(t => t is not Building).ToList();
                foreach (var thing in things)
                    thing.Destroy();
            }

            // Spawn one new xenogerm on the shelf's origin cell
            var xenogerm = (Xenogerm)ThingMaker.MakeThing(ThingDefOf.Xenogerm);
            SetXenogermGenes(xenogerm, genes, label, icon);
            GenSpawn.Spawn(xenogerm, shelf.Position, map);
        }
    }

    private static void SetXenogermGenes(Xenogerm xenogerm, List<GeneDef> genes, string label, XenotypeIconDef icon)
    {
        var geneSet = new GeneSet();
        foreach (var gene in genes)
            geneSet.AddGene(gene);

        GeneSetField.SetValue(xenogerm, geneSet);
        xenogerm.xenotypeName = label;
        xenogerm.iconDef = icon;
    }
}
