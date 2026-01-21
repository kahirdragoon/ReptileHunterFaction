using RimWorld;
using Verse;

namespace ReptileHunterFaction
{
    [DefOf]
    public static class VanillaThingDefOf
    {
        public static ThingDef Turret_Autocannon;
        public static ThingDef TrapIED_HighExplosive;
        public static ThingDef Meat_Megaspider;
        public static ThingDef DrugLab;
        public static ThingDef Neutroamine;

        static VanillaThingDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(VanillaThingDefOf));
    }
}
