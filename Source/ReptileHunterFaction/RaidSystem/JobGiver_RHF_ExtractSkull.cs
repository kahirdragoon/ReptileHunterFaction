using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ReptileHunterFaction;

/// <summary>
/// Gives the vanilla ExtractSkull job to a designated skull extractor.
/// Uses JobDefOf.ExtractSkull so the raider gets the full vanilla
/// extraction animation, sound, and Skull item creation.
/// </summary>
public class JobGiver_RHF_ExtractSkull : ThinkNode_JobGiver
{
    protected override Job? TryGiveJob(Pawn pawn)
    {
        if (pawn.GetLord()?.LordJob is not LordJob_RHF_KidnappingRaid lordJob || !lordJob.IsSkullExtractor(pawn)) return null;

        Pawn? victim = lordJob.NextSkullTarget(pawn);
        Corpse? corpse = victim?.Corpse;
        if (corpse == null || !corpse.Spawned) return null;

        Job job = JobMaker.MakeJob(JobDefOf.ExtractSkull, corpse);
        job.count = 1;
        return job;
    }
}
