using RimWorld;
using Verse;

namespace ReptileHunterFaction
{
    [DefOf]
    public static class ReptileHunterFactionDefOf
    {
        public static FactionDef RHF_ReptileHunters;
        public static MapGeneratorDef RHF_Faction;
        public static ThingDef RHF_SBD;
        public static ThingDef RHF_SBD_Extractor;
        public static ThingDef RHF_DrugMedicine;
        public static ThingDef RHF_Plant_DrugMedicine;
        public static ThingDef RHF_ConcentratedInsectoidBlood;

        static ReptileHunterFactionDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(ReptileHunterFactionDefOf));
    }
}
