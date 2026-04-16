using RimWorld;

namespace ReptileHunterFaction
{
    public class CompProperties_AncientComplexScanner : CompProperties_Scanner
    {
        public int minScansBeforeBreakdown = 5;
        public int maxScansBeforeBreakdown = 10;

        public CompProperties_AncientComplexScanner()
        {
            compClass = typeof(CompAncientComplexScanner);
        }
    }
}
