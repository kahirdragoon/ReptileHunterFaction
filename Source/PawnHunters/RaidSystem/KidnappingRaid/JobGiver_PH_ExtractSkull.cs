using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnHunters;

/// <summary>
/// Gives the vanilla ExtractSkull job to a designated skull extractor.
/// Uses JobDefOf.ExtractSkull so the raider gets the full vanilla
/// extraction animation, sound, and Skull item creation.
/// The vanilla job driver requires an ExtractSkull designation on the corpse
/// (it has a FailOn that checks for it); the LordJob adds and later removes it.
/// </summary>
public class JobGiver_PH_ExtractSkull : ThinkNode_JobGiver
{
    public override Job? TryGiveJob(Pawn pawn)
    {
        if (pawn.GetLord()?.LordJob is not LordJob_PH_KidnappingRaid lordJob) return null;

        // Duty without an extractor entry (e.g. after a load, which resets that state): rejoin the other raiders.
        if (!lordJob.IsSkullExtractor(pawn)) return PHRaidDutyUtility.ResetDutyAndThink(pawn, lordJob);

        Pawn? victim = lordJob.NextSkullTarget(pawn);
        Corpse? corpse = victim?.Corpse;
        if (corpse == null || !corpse.Spawned) return null; // LordToil_PH_Assault sends the extractor home

        lordJob.DesignateSkullExtraction(corpse);

        Job job = JobMaker.MakeJob(JobDefOf.ExtractSkull, corpse);
        job.count = 1;
        return job;
    }
}
