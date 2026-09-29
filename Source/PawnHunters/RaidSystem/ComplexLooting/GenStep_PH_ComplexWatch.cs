using Verse;

namespace PawnHunters;

/// <summary>
/// Linked to the AncientComplex site part via GenStepDef.linkWithSite, so it only runs when an
/// ancient complex map is generated (caravan arrival or gravship landing). Adds the looter watch.
/// </summary>
public class GenStep_PH_ComplexWatch : GenStep
{
    public override int SeedPart => 529184736;

    public override void Generate(Map map, GenStepParams parms)
    {
        if (map.GetComponent<MapComponent_PH_ComplexWatch>() == null)
            map.components.Add(new MapComponent_PH_ComplexWatch(map));
    }
}
