using RimWorld.Planet;
using Verse;

namespace PawnHunters;

/// <summary>
/// Tracks skulls collected by Pawn Hunter raiders from player pawns.
/// Each entry is the short name of the victim (used to call CompHasSources.AddSource
/// when placing Skull items or Skullspike buildings during settlement map generation).
/// </summary>
public class WorldComp_SpoilsOfBattle(World world) : WorldComponent(world)
{
    public List<string> skulls  = [];
    public List<Corpse> corpses = [];

    /// <summary>Qualifying gifted prisoners that buy off one raider.</summary>
    public const int PrisonersPerRaider = 2;

    /// <summary>
    /// Qualifying prisoners gifted to PH that no raid has used yet.
    /// Every 2 of them reduce a kidnapping raid by 1 raider.
    /// </summary>
    private int pendingQualifyingGiftCount;

    public int SkullCount => skulls.Count;

    public void AddSkull(string pawnName) => skulls.Add(pawnName);

    /// <summary>
    /// Stores a corpse carried off by a raid, without its gear: only the body is placed later (settlement butchery).
    /// The corpse only references its dead pawn, which WorldPawns saves and keeps as long as the corpse exists,
    /// so worn apparel, weapons and inventory would stay in every save.
    /// </summary>
    public void AddCorpse(Corpse corpse)
    {
        StripGear(corpse);
        corpses.Add(corpse);
    }

    private static void StripGear(Corpse corpse)
    {
        Pawn? inner = corpse.InnerPawn;
        if (inner == null) return;
        // The same calls vanilla makes when a corpse is destroyed (Corpse.PostCorpseDestroy).
        inner.apparel?.DestroyAll();
        inner.equipment?.DestroyAllEquipment();
        inner.inventory?.DestroyAll();
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        corpses.RemoveAll(c => c == null || c.Destroyed);
        foreach (Corpse corpse in corpses)
            StripGear(corpse); // corpses stored by older versions still wear their gear
        // Saves where the faction was already defeated before this cleanup existed.
        TryDiscardCorpsesAfterDefeat();
    }

    /// <summary>
    /// Stored corpses only ever go into Pawn Hunter settlements, so they are discarded once the faction is defeated
    /// (called by Patch_SettlementDefeat_PH when the last settlement falls, and on load).
    /// They are destroyed the way vanilla discards a corpse (PostCorpseDestroy with discarded: true): a plain
    /// Destroy would notify the ideoligion and give every stored colonist a new funeral obligation.
    /// </summary>
    internal void TryDiscardCorpsesAfterDefeat()
    {
        if (corpses.Count == 0) return;
        if (Find.FactionManager.FirstFactionOfDef(PawnHuntersDefOf.PH_PawnHunters) is not { defeated: true }) return;

        foreach (Corpse corpse in corpses)
        {
            if (corpse.Destroyed) continue;
            Pawn? inner = corpse.InnerPawn;
            corpse.GetDirectlyHeldThings().Clear(); // an empty corpse skips its own PostCorpseDestroy
            corpse.Destroy();
            if (inner != null)
                Corpse.PostCorpseDestroy(inner, discarded: true);
        }
        corpses.Clear();
    }

    /// <summary>Registers qualifying prisoners sent to PH as gifts.</summary>
    public void AddGiftedPrisoners(int qualifyingCount) =>
        pendingQualifyingGiftCount += qualifyingCount;

    /// <summary>Available raider reduction (1 per 2 qualifying prisoners) without spending it.</summary>
    public int PeekRaidDiscount() => pendingQualifyingGiftCount / PrisonersPerRaider;

    /// <summary>
    /// Spends up to <paramref name="maxRaiders"/> of the raider reduction and returns how many raiders were removed.
    /// Only the prisoners actually used are taken; the rest (including an odd one) stay for the next raid.
    /// Call once when a kidnapping raid actually fires.
    /// </summary>
    public int ConsumeRaidDiscount(int maxRaiders)
    {
        int discount = Math.Min(PeekRaidDiscount(), Math.Max(0, maxRaiders));
        pendingQualifyingGiftCount -= discount * PrisonersPerRaider;
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
