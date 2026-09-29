using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnHunters;

/// <summary>
/// Issues a PH_KidnapAndFlee job to the pawn that has been assigned as a kidnapper
/// in the big kidnapping raid. Mirrors JobGiver_PH_KidnapDowned but casts to
/// LordJob_PH_KidnappingRaidBig.
/// </summary>
public class JobGiver_PH_KidnapDowned_Big : ThinkNode_JobGiver
{
    public override Job? TryGiveJob(Pawn pawn)
    {
        if (pawn.GetLord()?.LordJob is not LordJob_PH_KidnappingRaidBig lordJob) return null;

        Pawn? target = lordJob.GetTargetFor(pawn);
        if (target != null && !target.Dead && target.Downed && target.Spawned)
        {
            Job job = JobMaker.MakeJob(PawnHuntersDefOf.PH_KidnapAndFlee, target);
            job.count = 1;
            return job;
        }

        // Kidnap over (target rescued or dead, or the kidnap job ended early): rejoin the other raiders.
        lordJob.OnKidnapComplete(pawn);
        return PHRaidDutyUtility.ResetDutyAndThink(pawn, lordJob);
    }
}
