using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Issues a RHF_KidnapAndFlee job to the pawn that has been assigned as a kidnapper
/// in the big kidnapping raid. Mirrors JobGiver_RHF_KidnapDowned but casts to
/// LordJob_RHF_KidnappingRaidBig.
/// </summary>
public class JobGiver_RHF_KidnapDowned_Big : ThinkNode_JobGiver
{
    protected override Job? TryGiveJob(Pawn pawn)
    {
        var lordJob = pawn.GetLord()?.LordJob as LordJob_RHF_KidnappingRaidBig;
        if (lordJob == null || !lordJob.IsKidnapper(pawn)) return null;

        Pawn? target = lordJob.GetTargetFor(pawn);
        if (target == null || target.Dead || !target.Downed) return null;

        Job job = JobMaker.MakeJob(ReptileHunterFactionDefOf.RHF_KidnapAndFlee, target);
        job.count = 1;
        return job;
    }
}
