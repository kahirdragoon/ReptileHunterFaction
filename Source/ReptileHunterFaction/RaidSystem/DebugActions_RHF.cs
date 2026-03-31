using LudeonTK;
using RimWorld;
using Verse;

namespace ReptileHunterFaction;

public static class DebugActions_RHF
{
    [DebugAction("RHF", "Kidnapping raid (pick points)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static List<DebugActionNode> TriggerKidnappingRaid()
    {
        var nodes = new List<DebugActionNode>();
        foreach (float pts in DebugActionsUtility.PointsOptions(extended: true))
        {
            float localPts = pts;
            nodes.Add(new DebugActionNode($"{localPts} points", DebugActionType.Action, () =>
            {
                IncidentDef def = DefDatabase<IncidentDef>.GetNamed("RHF_KidnappingRaid");
                def.Worker.TryExecute(new IncidentParms
                {
                    target = Find.CurrentMap,
                    points = localPts,
                    forced = true
                });
            }));
        }
        return nodes;
    }
}
