using Verse;

namespace PawnHunters;

/// <summary>
/// Shared contract for kidnapping LordJobs so that JobDriver_PH_KidnapAndFlee
/// can call OnKidnapComplete without coupling to a specific LordJob subclass.
/// </summary>
internal interface IKidnappingLordJob
{
    void OnKidnapComplete(Pawn kidnapper);
}
