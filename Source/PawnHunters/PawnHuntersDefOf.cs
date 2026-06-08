using RimWorld;
using Verse;
using Verse.AI;

namespace PawnHunters
{
    [DefOf]
    public static class PawnHuntersDefOf
    {
        public static FactionDef PH_PawnHunters;
        public static MapGeneratorDef PH_Faction;
        public static ThingDef PH_BP;
        public static ThingDef PH_BP_Extractor;
        public static ThingDef PH_DrugMedicine;
        public static ThingDef PH_Plant_DrugMedicine;
        public static ThingDef PH_BiologicalExtract;
        public static PawnKindDef PH_PawnHuntersFighter_Ranged;
        public static PawnKindDef PH_PawnHuntersFighter_Melee;
        public static PawnKindDef PH_PawnHuntersFighter_Boss;

        // Kidnapping raid
        public static RaidStrategyDef PH_KidnappingRaidStrategy;
        public static DutyDef PH_KidnaperDuty;
        public static JobDef PH_KidnapAndFlee;

        // Skull extraction
        public static DutyDef PH_SkullExtractorDuty;

        // Big kidnapping raid
        public static RaidStrategyDef PH_KidnappingRaidStrategy_Big;
        public static RaidStrategyDef PH_KidnappingRaidStrategy_Boss;
        public static DutyDef         PH_KidnaperDuty_Big;
        public static DutyDef         PH_CarryCorpseDuty;
        public static JobDef          PH_CarryCorpseOffMap;

        // Complex looting raid
        public static DutyDef PH_ComplexLooterDuty;
        public static JobDef  PH_ExploreRoom;

        static PawnHuntersDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(PawnHuntersDefOf));
    }
}
