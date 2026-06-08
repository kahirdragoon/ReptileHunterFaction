using RimWorld;
using RimWorld.Planet;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI.Group;

namespace PawnHunters;

/// <summary>
/// Injected (via Harmony) onto maps that are ancient complex sites.
/// Waits 1–1.5 in-game hours after the player arrives, then has a 1-in-4 chance
/// to spawn 1–2 Hunter Faction scouts who loot the complex.
/// </summary>
public class MapComponent_PH_ComplexWatch(Map map) : MapComponent(map)
{
    private bool _raidSpawned   = false;
    private int  _raidTick      = -1;
    private bool _initialized   = false;

    private bool IsValidMap =>
        !map.IsPlayerHome
        && map.Parent is Site site
        && site.parts.Any(p => p.def.tags?.Contains("AncientComplex") == true);

    public override void FinalizeInit()
    {
        base.FinalizeInit();

        if (!IsValidMap)
            map.components.Remove(this);
    }

    public override void MapComponentTick()
    {
        if (_raidSpawned) return;

        if (!_initialized)
        {
            _initialized = true;
            _raidTick    = Find.TickManager.TicksGame + Rand.Range(2500, 3750);
            return;
        }

        if (Find.TickManager.TicksGame >= _raidTick)
        {
            _raidSpawned = true;
            TrySpawnRaid();
        }
    }

    public void TrySpawnRaid()
    {
        if (!Rand.Chance(0.25f)) return;

        Faction? faction = Find.FactionManager.FirstFactionOfDef(PawnHuntersDefOf.PH_PawnHunters);
        if (faction == null) return;

        // Count player + prisoner pawns on map to determine raider count
        int playerPawnCount = map.mapPawns.AllPawnsSpawned
            .Count(p => !p.Dead && p.RaceProps.Humanlike
                      && (p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony));
        int raiderCount = playerPawnCount >= 4 ? 2 : 1;

        // Spawn at map edge
        if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entryCell, map, 8f))
            return;

        var spawnedPawns = new List<Pawn>();
        for (int i = 0; i < raiderCount; i++)
        {
            PawnKindDef kindDef = Rand.Bool
                ? PawnHuntersDefOf.PH_PawnHuntersFighter_Ranged
                : PawnHuntersDefOf.PH_PawnHuntersFighter_Melee;

            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kindDef, faction));
            GenSpawn.Spawn(pawn, entryCell, map);
            spawnedPawns.Add(pawn);
        }

        if (spawnedPawns.Count == 0) return;

        LordMaker.MakeNewLord(faction, new LordJob_PH_ComplexLooting(), map, spawnedPawns);

        Find.LetterStack.ReceiveLetter(
            "PH_ComplexLooters_LetterLabel".Translate(),
            "PH_ComplexLooters_LetterText".Translate(),
            LetterDefOf.NeutralEvent,
            new LookTargets(spawnedPawns[0]));
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _raidSpawned, "raidSpawned", false);
        Scribe_Values.Look(ref _raidTick,    "raidTick",    -1);
        Scribe_Values.Look(ref _initialized, "initialized", false);
    }
}
