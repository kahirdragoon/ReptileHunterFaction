using Verse;

namespace PawnHunters;

/// <summary>
/// Shared contract for kidnapping LordJobs so that JobDriver_PH_KidnapAndFlee and the special-duty
/// job givers can talk to either raid's LordJob without coupling to a specific subclass.
/// </summary>
internal interface IKidnappingLordJob
{
    void OnKidnapComplete(Pawn kidnapper);

    /// <summary>True if a kidnapper is already assigned to this pawn.</summary>
    bool IsTargeted(Pawn pawn);

    /// <summary>Puts a raider whose special assignment ended back on the duty free raiders have in the current toil.</summary>
    void ResetToFreeDuty(Pawn pawn);
}
