using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnHunters;

/// <summary>
/// Job giver for the PH_KidnaperDuty duty.
/// Only fires for pawns designated as kidnappers by LordJob_PH_KidnappingRaid.
/// </summary>
public class JobGiver_PH_KidnapDowned : ThinkNode_JobGiver
{
    protected override Job? TryGiveJob(Pawn pawn)
    {
        if (pawn.GetLord()?.LordJob is not LordJob_PH_KidnappingRaid lordJob) return null;

        Pawn? target = lordJob.GetTargetFor(pawn);

        // Already carrying the victim, which despawns it: keep the running kidnap job. The think tree also runs
        // mid-job (CheckForJobOverride), and resetting the duty here would end the job and drop the victim.
        if (target != null && pawn.carryTracker.CarriedThing == target && pawn.CurJobDef == PawnHuntersDefOf.PH_KidnapAndFlee)
            return pawn.CurJob;

        if (target != null && !target.Dead && target.Downed && target.Spawned)
        {
            Job job = JobMaker.MakeJob(PawnHuntersDefOf.PH_KidnapAndFlee, target);
            job.count = 1; // required by Toils_Haul.StartCarryThing
            return job;
        }

        // Kidnap over (target rescued or dead, or the kidnap job ended early): rejoin the other raiders.
        lordJob.OnKidnapComplete(pawn);
        return PHRaidDutyUtility.ResetDutyAndThink(pawn, lordJob);
    }
}
