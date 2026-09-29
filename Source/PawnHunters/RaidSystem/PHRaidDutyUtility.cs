using Verse;
using Verse.AI;

namespace PawnHunters;

internal static class PHRaidDutyUtility
{
    /// <summary>
    /// For the job givers of the special raid duties (kidnapper, corpse carrier, skull extractor) when the pawn has
    /// nothing left to do on that duty: resets it to the toil's free duty and returns that duty's job right away.
    /// Returning null instead falls through the Humanlike think tree to its final JobGiver_ExitMapBest,
    /// so the raider would quietly walk off the map.
    /// </summary>
    public static Job? ResetDutyAndThink(Pawn pawn, IKidnappingLordJob lordJob)
    {
        lordJob.ResetToFreeDuty(pawn);
        return pawn.mindState.duty?.def.thinkNode?.TryIssueJobPackage(pawn, default).Job;
    }
}
