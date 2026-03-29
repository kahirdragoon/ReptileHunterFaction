using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using Verse;
using Verse.Grammar;

namespace ReptileHunterFaction;

public class SitePartWorker_RHFPrison : SitePartWorker
{
    public override bool FactionCanOwn(Faction faction)
    {
        // Only the Hunter faction can own this site part
        return faction?.def.defName == "RHF_ReptileHunters";
    }

    public override void Notify_GeneratedByQuestGen(
        SitePart part,
        Slate slate,
        List<Rule> outExtraDescriptionRules,
        Dictionary<string, string> outExtraDescriptionConstants)
    {
        base.Notify_GeneratedByQuestGen(part, slate, outExtraDescriptionRules, outExtraDescriptionConstants);

        // Get the kidnapped pawn that was selected by the quest node
        if (!slate.TryGet<Pawn>("kidnappedPawn", out Pawn kidnappedPawn))
            return;

        // Persist the pawn on the site part so it is available at map generation time.
        part.things = new ThingOwner<Pawn>(part, oneStackOnly: true);
        part.things.TryAdd(kidnappedPawn);

        // Keep the selected pawn available in slate under the quest-specific key.
        slate.Set("kidnappedPawn", kidnappedPawn);

        // Generate grammar rules just like the vanilla PrisonerWillingToJoin does
        outExtraDescriptionRules.Add(new Rule_String("kidnappedPawn_nameDef", kidnappedPawn.Name.ToStringShort));
        outExtraDescriptionRules.Add(new Rule_String("kidnappedPawn_pronoun", GetPronoun(kidnappedPawn)));
        outExtraDescriptionRules.Add(new Rule_String("kidnappedPawn_objective", GetObjectivePronoun(kidnappedPawn)));
        outExtraDescriptionRules.Add(new Rule_String("kidnappedPawn_age", kidnappedPawn.ageTracker.AgeNumberString));
        outExtraDescriptionRules.Add(new Rule_String("kidnappedPawn_title", kidnappedPawn.story?.title ?? "colonist"));

        // Get relation info
        string relationInfo = "";
        PawnRelationUtility.Notify_PawnsSeenByPlayer(Gen.YieldSingle(kidnappedPawn), out string pawnRelationsInfo, true, false);
        if (!pawnRelationsInfo.NullOrEmpty())
        {
            relationInfo = string.Format("\n\n{0}\n\n{1}", 
                "PawnHasTheseRelationshipsWithColonists".Translate(kidnappedPawn.LabelShort, (Thing)kidnappedPawn),
                pawnRelationsInfo);
        }
        outExtraDescriptionRules.Add(new Rule_String("kidnappedPawnFullRelationInfo", relationInfo));
    }

    private string GetPronoun(Pawn pawn)
    {
        return pawn.gender switch
        {
            Gender.Female => "she",
            Gender.Male => "he",
            _ => "they"
        };
    }

    private string GetObjectivePronoun(Pawn pawn)
    {
        return pawn.gender switch
        {
            Gender.Female => "her",
            Gender.Male => "him",
            _ => "them"
        };
    }
}

