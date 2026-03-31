using RimWorld.Planet;
using Verse;

namespace ReptileHunterFaction;

/// <summary>
/// Tracks skulls collected by Reptile Hunter raiders from player pawns.
/// Each entry is the short name of the victim (used to call CompHasSources.AddSource
/// when placing Skull items or Skullspike buildings during settlement map generation).
/// </summary>
public class WorldComp_RHFSkulls : WorldComponent
{
    public List<string> skulls = [];

    public int SkullCount => skulls.Count;

    public WorldComp_RHFSkulls(World world) : base(world) { }

    public void AddSkull(string pawnName) => skulls.Add(pawnName);

    public static WorldComp_RHFSkulls? Get() =>
        Find.World.GetComponent<WorldComp_RHFSkulls>();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref skulls, "skulls", LookMode.Value);
        skulls ??= [];
    }
}
