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

    public int SkullCount => skulls.Count;

    public void AddSkull(string pawnName) => skulls.Add(pawnName);
    public void AddCorpse(Corpse corpse)  => corpses.Add(corpse);

    public static WorldComp_SpoilsOfBattle? Get() =>
        Find.World.GetComponent<WorldComp_SpoilsOfBattle>();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref skulls,  "skulls",  LookMode.Value);
        Scribe_Collections.Look(ref corpses, "corpses", LookMode.Deep);
        skulls  ??= [];
        corpses ??= [];
    }
}
