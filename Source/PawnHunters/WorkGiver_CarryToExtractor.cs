using RimWorld;
using Verse;

namespace PawnHunters;

/// <summary>
/// Lets haulers carry the selected prisoner or downed pawn into the bloodprime extractor.
/// Vanilla's carry work givers only match their own buildings (gene extractor, growth vat, subcore scanners).
/// </summary>
public class WorkGiver_CarryToExtractor : WorkGiver_CarryToBuilding
{
    public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(PawnHuntersDefOf.PH_BP_Extractor);
}
