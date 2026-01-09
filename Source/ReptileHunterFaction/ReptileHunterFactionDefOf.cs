using RimWorld;
using Verse;

namespace ReptileHunterFaction
{
    [DefOf]
    public static class ReptileHunterFactionDefOf
    {
        public static FactionDef RHF_ReptileHunters;
        public static ThingDef RHF_SBD;

        static ReptileHunterFactionDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(ReptileHunterFactionDefOf));
    }
}
