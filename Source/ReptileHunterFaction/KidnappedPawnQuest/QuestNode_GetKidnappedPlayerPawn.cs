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

    protected override bool TestRunInt(Slate slate) => true;

    protected override void RunInt()
    {
        var faction = factionRef.GetValue(QuestGen.slate);
        // If a kidnapped pawn exists, store it in the slate so downstream nodes can use it.
        // If none exists the slate key stays unset (null), and QuestNode_IsNull in the quest
        // XML conditionally skips the pawn reward node.
        if (TryGetKidnappedPawn(faction, out Pawn? pawn))
            QuestGen.slate.Set(storeAs.GetValue(QuestGen.slate), pawn);
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
