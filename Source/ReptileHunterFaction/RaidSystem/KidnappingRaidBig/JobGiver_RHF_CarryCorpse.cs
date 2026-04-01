using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Issues a RHF_CarryCorpseOffMap job to the raider designated as a corpse carrier
/// in the big kidnapping raid.
/// </summary>
public class JobGiver_RHF_CarryCorpse : ThinkNode_JobGiver
{
    protected override Job? TryGiveJob(Pawn pawn)
    {
        var lordJob = pawn.GetLord()?.LordJob as LordJob_RHF_KidnappingRaidBig;
        if (lordJob == null || !lordJob.IsCorpseCarrier(pawn)) return null;

        Corpse? corpse = lordJob.GetCorpseFor(pawn);
        if (corpse == null || !corpse.Spawned || corpse.Map != pawn.Map) return null;

        Job job = JobMaker.MakeJob(ReptileHunterFactionDefOf.RHF_CarryCorpseOffMap, corpse);
        job.count = 1;
        return job;
    }
}
