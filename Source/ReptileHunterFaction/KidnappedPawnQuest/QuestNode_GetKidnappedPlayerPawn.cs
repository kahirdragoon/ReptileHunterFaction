using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ReptileHunterFaction;

public class QuestNode_GetKidnappedPlayerPawn : QuestNode
{
    [NoTranslate]
    public SlateRef<string> storeAs = new("kidnappedPawn");

    [NoTranslate]
    public SlateRef<Faction> factionRef = new("siteFaction");

    protected override bool TestRunInt(Slate slate)
    {
        var faction = factionRef.GetValue(slate);
        return AreThereKidnappedPawns(faction);
    }

    protected override void RunInt()
    {
        var faction = factionRef.GetValue(QuestGen.slate);
        if (!TryGetKidnappedPawn(faction, out Pawn? pawn))
        {
            Log.Error("QuestNode_GetKidnappedPlayerPawn: could not find a kidnapped player pawn for faction " + faction?.Name);
            return;
        }
        QuestGen.slate.Set(storeAs.GetValue(QuestGen.slate), pawn);
    }

    private bool AreThereKidnappedPawns(Faction kidnappingFaction)
    {
        if (kidnappingFaction?.kidnapped == null)
            return false;

        int count = kidnappingFaction.kidnapped.KidnappedPawnsListForReading?.Count ?? 0;
        return count > 0;
    }

    private bool TryGetKidnappedPawn(Faction kidnappingFaction, out Pawn? pawn)
    {
        pawn = null;
        
        if (kidnappingFaction?.kidnapped?.KidnappedPawnsListForReading == null)
            return false;

        var validKidnappedPawns = kidnappingFaction.kidnapped.KidnappedPawnsListForReading
            .Where(p => p != null && !p.DestroyedOrNull() && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer)
            .ToList();

        return validKidnappedPawns.TryRandomElement(out pawn);
    }
}
