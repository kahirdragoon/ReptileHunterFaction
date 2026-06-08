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
    public override Job? TryGiveJob(Pawn pawn)
    {
        var lordJob = pawn.GetLord()?.LordJob as LordJob_PH_KidnappingRaid;
        if (lordJob == null || !lordJob.IsKidnapper(pawn)) return null;

        Pawn? target = lordJob.GetTargetFor(pawn);
        if (target == null || target.Dead || !target.Downed) return null;

        Job job = JobMaker.MakeJob(PawnHuntersDefOf.PH_KidnapAndFlee, target);
        job.count = 1; // required by Toils_Haul.StartCarryThing
        return job;
    }
}
