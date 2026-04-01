using Verse;

namespace ReptileHunterFaction;

/// <summary>
/// Shared contract for kidnapping LordJobs so that JobDriver_RHF_KidnapAndFlee
/// can call OnKidnapComplete without coupling to a specific LordJob subclass.
/// </summary>
internal interface IKidnappingLordJob
{
    void OnKidnapComplete(Pawn kidnapper);
}
