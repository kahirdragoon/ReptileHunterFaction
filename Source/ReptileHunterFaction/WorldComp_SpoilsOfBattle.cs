using RimWorld.Planet;
using Verse;

namespace ReptileHunterFaction;

/// <summary>
/// Tracks skulls collected by Reptile Hunter raiders from player pawns.
/// Each entry is the short name of the victim (used to call CompHasSources.AddSource
/// when placing Skull items or Skullspike buildings during settlement map generation).
/// </summary>
public class WorldComp_SpoilsOfBattle(World world) : WorldComponent(world)
{
    public List<string> skulls  = [];
    public List<Corpse> corpses = [];

    /// <summary>
    /// Number of qualifying prisoners gifted to RHF since the last kidnapping raid.
    /// Every 2 qualifying prisoners reduces the next kidnapping raid by 1 raider.
    /// </summary>
    private int pendingQualifyingGiftCount;

    public int SkullCount => skulls.Count;

    public void AddSkull(string pawnName) => skulls.Add(pawnName);
    public void AddCorpse(Corpse corpse)  => corpses.Add(corpse);

    /// <summary>Registers qualifying prisoners sent to RHF as gifts.</summary>
    public void AddGiftedPrisoners(int qualifyingCount) =>
        pendingQualifyingGiftCount += qualifyingCount;

    /// <summary>
    /// Returns the raider reduction (1 per 2 qualifying prisoners) and resets the counter.
    /// Call once when a kidnapping raid actually fires.
    /// </summary>
    public int ConsumeRaidDiscount()
    {
        int discount = pendingQualifyingGiftCount / 2;
        pendingQualifyingGiftCount = 0;
        return discount;
    }

    public static WorldComp_SpoilsOfBattle? Get() =>
        Find.World.GetComponent<WorldComp_SpoilsOfBattle>();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref skulls,  "skulls",  LookMode.Value);
        Scribe_Collections.Look(ref corpses, "corpses", LookMode.Deep);
        Scribe_Values.Look(ref pendingQualifyingGiftCount, "pendingQualifyingGiftCount");
        skulls  ??= [];
        corpses ??= [];
    }
}
