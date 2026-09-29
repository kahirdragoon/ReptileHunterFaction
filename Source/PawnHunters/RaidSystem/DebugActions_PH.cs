using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PawnHunters;

public static class DebugActions_PH
{
    [DebugAction("PH", "Kidnapping raid", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void TriggerKidnappingRaid()
    {
        // Points are ignored by the small raid but must be non-zero to pass vanilla validation.
        DefDatabase<IncidentDef>.GetNamed("PH_KidnappingRaid").Worker.TryExecute(new IncidentParms
        {
            target = Find.CurrentMap,
            forced = true,
            points = StorytellerUtility.DefaultThreatPointsNow(Find.CurrentMap)
        });
    }

    // Returns a node (instead of being a plain action) so it can be hidden via visibilityGetter:
    // only listed while the current map is an ancient complex site.
    [DebugAction("PH", "Spawn complex looters", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static DebugActionNode SpawnComplexLooters() => new(null, DebugActionType.Action, () =>
    {
        Map map = Find.CurrentMap;
        var comp = map.GetComponent<MapComponent_PH_ComplexWatch>();
        if (comp == null)
        {
            comp = new MapComponent_PH_ComplexWatch(map);
            map.components.Add(comp);
        }
        comp.TrySpawnRaid();
    })
    {
        visibilityGetter = () => Find.CurrentMap?.Parent is Site site
                                 && site.parts.Any(p => p.def == SitePartDefOf.AncientComplex)
    };

    [DebugAction("PH", "Kidnapping raid (big)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static List<DebugActionNode> TriggerKidnappingRaidBig()
    {
        var nodes = new List<DebugActionNode>();
        foreach (float pts in DebugActionsUtility.PointsOptions(extended: true))
        {
            float localPts = pts;
            nodes.Add(new DebugActionNode(localPts + " points")
            {
                action = () => DefDatabase<IncidentDef>.GetNamed("PH_KidnappingRaid_Big").Worker.TryExecute(new IncidentParms
                {
                    target = Find.CurrentMap,
                    forced = true,
                    points = localPts
                })
            });
        }
        return nodes;
    }

    [DebugAction("PH", "Kidnapping raid (boss)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static List<DebugActionNode> TriggerKidnappingRaidBoss()
    {
        var nodes = new List<DebugActionNode>();
        foreach (float pts in DebugActionsUtility.PointsOptions(extended: true))
        {
            float localPts = pts;
            nodes.Add(new DebugActionNode(localPts + " points")
            {
                action = () => DefDatabase<IncidentDef>.GetNamed("PH_KidnappingRaid_Boss").Worker.TryExecute(new IncidentParms
                {
                    target = Find.CurrentMap,
                    forced = true,
                    points = localPts
                })
            });
        }
        return nodes;
    }

    [DebugAction("PH", "Fire kidnapped pawn quest", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void FireKidnappedPawnQuest()
    {
        var questDef = DefDatabase<QuestScriptDef>.GetNamed("PH_OpportunitySite_KidnappedPawnPrison");
        var quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, StorytellerUtility.DefaultThreatPointsNow(Find.CurrentMap));
        if (!quest.hidden && quest.root.sendAvailableLetter)
            QuestUtility.SendLetterQuestAvailable(quest);
    }
}
