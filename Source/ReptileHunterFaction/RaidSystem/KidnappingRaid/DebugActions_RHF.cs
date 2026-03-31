using LudeonTK;
using RimWorld;
using Verse;

namespace ReptileHunterFaction;

public static class DebugActions_RHF
{
    [DebugAction("RHF", "Kidnapping raid", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void TriggerKidnappingRaid()
    {
        DefDatabase<IncidentDef>.GetNamed("RHF_KidnappingRaid").Worker.TryExecute(new IncidentParms
        {
            target = Find.CurrentMap,
            forced = true
        });
    }
}
