using RimWorld;
using Verse;

namespace ReptileHunterFaction
{
    [DefOf]
    public static class VanillaDefOf
    {
        public static ThingDef Turret_Autocannon;
        public static ThingDef TrapIED_HighExplosive;
        public static ThingDef Meat_Megaspider;
        public static ThingDef DrugLab;
        public static ThingDef Neutroamine;

        static VanillaDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(VanillaDefOf));
    }
}
