using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace PawnHunters
{
    public class CompAncientComplexScanner : CompScanner
    {
        private int successfulScans;
        private int maxScans;
        private bool burnedOut;

        public new CompProperties_AncientComplexScanner Props => (CompProperties_AncientComplexScanner)props;

        public override AcceptanceReport CanUseNow
        {
            get
            {
                if (burnedOut)
                    return "PH_ScannerBurnedOut".Translate();
                return base.CanUseNow;
            }
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            maxScans = Rand.RangeInclusive(Props.minScansBeforeBreakdown, Props.maxScansBeforeBreakdown);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref successfulScans, "successfulScans");
            Scribe_Values.Look(ref maxScans, "maxScans");
            Scribe_Values.Look(ref burnedOut, "burnedOut");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && maxScans <= 0)
                maxScans = Rand.RangeInclusive(Props.minScansBeforeBreakdown, Props.maxScansBeforeBreakdown);
        }

        public override void DoFind(Pawn worker)
        {
            QuestScriptDef questScript = DefDatabase<QuestScriptDef>.GetNamedSilentFail("PH_AncientComplexScan");

            Slate slate = new Slate();
            slate.Set("map", parent.Map);
            slate.Set("points", StorytellerUtility.DefaultSiteThreatPointsNow());

            if (questScript != null && questScript.CanRun(slate, parent.Map))
            {
                Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questScript, slate);
                Find.LetterStack.ReceiveLetter(quest.name, quest.description, LetterDefOf.PositiveEvent, (LookTargets)null, quest: quest);
            }
            else
            {
                // Fallback for when the Ideology DLC is not installed — place the site directly
                if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, 2, 10))
                {
                    Messages.Message("PH_ScannerNoSiteFound".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }
                Site site = SiteMaker.MakeSite(SitePartDefOf.AncientComplex, tile, Faction.OfAncients);
                Find.WorldObjects.Add(site);
                Find.LetterStack.ReceiveLetter(
                    "PH_AncientComplexFound_Label".Translate(),
                    "PH_AncientComplexFound_Desc".Translate(worker.LabelShort),
                    LetterDefOf.PositiveEvent,
                    new LookTargets(site));
            }

            successfulScans++;
            if (successfulScans >= maxScans)
            {
                burnedOut = true;
                Messages.Message("PH_ScannerBurnedOutMsg".Translate(parent.LabelCap), parent, MessageTypeDefOf.NegativeEvent);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (burnedOut)
                return "PH_ScannerBurnedOut".Translate();

            string baseStr = base.CompInspectStringExtra();
            if (!Prefs.DevMode)
                return baseStr;
            string scansLeft = "PH_ScansRemaining".Translate(maxScans - successfulScans);
            return baseStr.NullOrEmpty() ? scansLeft : baseStr + "\n" + scansLeft;
        }
    }
}
