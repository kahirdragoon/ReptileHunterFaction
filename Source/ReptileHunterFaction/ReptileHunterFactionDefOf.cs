using RimWorld;
using Verse;
using Verse.AI;

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
        public static PawnKindDef RHF_ReptileHuntersFighter_Ranged;
        public static PawnKindDef RHF_ReptileHuntersFighter_Melee;

        // Kidnapping raid
        public static RaidStrategyDef RHF_KidnappingRaidStrategy;
        public static DutyDef RHF_KidnaperDuty;
        public static JobDef RHF_KidnapAndFlee;

        // Skull extraction
        public static DutyDef RHF_SkullExtractorDuty;

        static ReptileHunterFactionDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(ReptileHunterFactionDefOf));
    }
}
