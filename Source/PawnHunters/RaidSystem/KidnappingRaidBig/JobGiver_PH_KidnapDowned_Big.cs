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
    protected override Job? TryGiveJob(Pawn pawn)
    {
        if (pawn.GetLord()?.LordJob is not LordJob_PH_KidnappingRaidBig lordJob) return null;

        Pawn? target = lordJob.GetTargetFor(pawn);

        // Already carrying the victim, which despawns it: keep the running kidnap job. The think tree also runs
        // mid-job (CheckForJobOverride), and resetting the duty here would end the job and drop the victim.
        if (target != null && pawn.carryTracker.CarriedThing == target && pawn.CurJobDef == PawnHuntersDefOf.PH_KidnapAndFlee)
            return pawn.CurJob;

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
