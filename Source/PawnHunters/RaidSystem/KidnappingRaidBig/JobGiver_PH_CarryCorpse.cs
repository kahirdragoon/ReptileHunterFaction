using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnHunters;

/// <summary>
/// Issues a PH_CarryCorpseOffMap job to the raider designated as a corpse carrier
/// in the big kidnapping raid.
/// </summary>
public class JobGiver_PH_CarryCorpse : ThinkNode_JobGiver
{
    protected override Job? TryGiveJob(Pawn pawn)
    {
        if (pawn.GetLord()?.LordJob is not LordJob_PH_KidnappingRaidBig lordJob) return null;

        // If another pawn already reserved the corpse, drop the stale assignment rather than
        // creating a job that will immediately log a reservation conflict.
        Corpse? corpse = lordJob.GetCorpseFor(pawn);

        // Already carrying the corpse, which despawns it: keep the running carry job. The think tree also runs
        // mid-job (CheckForJobOverride), and resetting the duty here would end the job and drop the corpse.
        if (corpse != null && pawn.carryTracker.CarriedThing == corpse && pawn.CurJobDef == PawnHuntersDefOf.PH_CarryCorpseOffMap)
            return pawn.CurJob;

        if (corpse != null && corpse.Spawned && corpse.Map == pawn.Map && pawn.CanReserve(corpse, 1, -1, null, false))
        {
            Job job = JobMaker.MakeJob(PawnHuntersDefOf.PH_CarryCorpseOffMap, corpse);
            job.count = 1;
            return job;
        }

        // Carry over (corpse gone or taken, or the carry job ended early): rejoin the other raiders.
        lordJob.OnCorpseCarryComplete(pawn);
        return PHRaidDutyUtility.ResetDutyAndThink(pawn, lordJob);
    }
}
