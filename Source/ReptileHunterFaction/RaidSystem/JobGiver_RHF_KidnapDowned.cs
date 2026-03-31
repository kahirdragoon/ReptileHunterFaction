using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Job giver for the RHF_KidnaperDuty duty.
/// Only fires for pawns designated as kidnappers by LordJob_RHF_KidnappingRaid.
/// </summary>
public class JobGiver_RHF_KidnapDowned : ThinkNode_JobGiver
{
    protected override Job? TryGiveJob(Pawn pawn)
    {
        var lordJob = pawn.GetLord()?.LordJob as LordJob_RHF_KidnappingRaid;
        if (lordJob == null || !lordJob.IsKidnapper(pawn)) return null;

        Pawn? target = lordJob.GetTargetFor(pawn);
        if (target == null || target.Dead || !target.Downed) return null;

        Job job = JobMaker.MakeJob(ReptileHunterFactionDefOf.RHF_KidnapAndFlee, target);
        job.count = 1; // required by Toils_Haul.StartCarryThing
        return job;
    }
}
