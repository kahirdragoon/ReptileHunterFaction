using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ReptileHunterFaction;

public static class DebugActions_RHF
{
    [DebugAction("RHF", "Kidnapping raid", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void TriggerKidnappingRaid()
    {
        // Points are ignored by the small raid but must be non-zero to pass vanilla validation.
        DefDatabase<IncidentDef>.GetNamed("RHF_KidnappingRaid").Worker.TryExecute(new IncidentParms
        {
            target = Find.CurrentMap,
            forced = true,
            points = StorytellerUtility.DefaultThreatPointsNow(Find.CurrentMap)
        });
    }

    [DebugAction("RHF", "Spawn complex looters", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnComplexLooters()
    {
        var comp = Find.CurrentMap.components.OfType<MapComponent_RHF_ComplexWatch>().FirstOrDefault();
        if (comp == null)
        {
            comp = new MapComponent_RHF_ComplexWatch(Find.CurrentMap);
            Find.CurrentMap.components.Add(comp);
        }
        comp.TrySpawnRaid();
    }

    [DebugAction("RHF", "Kidnapping raid (big)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static List<DebugActionNode> TriggerKidnappingRaidBig()
    {
        var nodes = new List<DebugActionNode>();
        foreach (float pts in DebugActionsUtility.PointsOptions(extended: true))
        {
            float localPts = pts;
            nodes.Add(new DebugActionNode(localPts + " points")
            {
                action = () => DefDatabase<IncidentDef>.GetNamed("RHF_KidnappingRaid_Big").Worker.TryExecute(new IncidentParms
                {
                    target = Find.CurrentMap,
                    forced = true,
                    points = localPts
                })
            });
        }
        return nodes;
    }

    [DebugAction("RHF", "Kidnapping raid (boss)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static List<DebugActionNode> TriggerKidnappingRaidBoss()
    {
        var nodes = new List<DebugActionNode>();
        foreach (float pts in DebugActionsUtility.PointsOptions(extended: true))
        {
            float localPts = pts;
            nodes.Add(new DebugActionNode(localPts + " points")
            {
                action = () => DefDatabase<IncidentDef>.GetNamed("RHF_KidnappingRaid_Boss").Worker.TryExecute(new IncidentParms
                {
                    target = Find.CurrentMap,
                    forced = true,
                    points = localPts
                })
            });
        }
        return nodes;
    }

    [DebugAction("RHF", "Fire kidnapped pawn quest", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void FireKidnappedPawnQuest()
    {
        var questDef = DefDatabase<QuestScriptDef>.GetNamed("RHF_OpportunitySite_KidnappedPawnPrison");
        var quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, StorytellerUtility.DefaultThreatPointsNow(Find.CurrentMap));
        if (!quest.hidden && quest.root.sendAvailableLetter)
            QuestUtility.SendLetterQuestAvailable(quest);
    }
}
