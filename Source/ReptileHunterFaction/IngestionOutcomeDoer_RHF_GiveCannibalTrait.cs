using RimWorld;
using Verse;

namespace ReptileHunterFaction
{
    /// <summary>
    /// On ingestion, gives the pawn the Cannibal gene (defName="Cannibal", from redmattis.bigsmall.core)
    /// if that gene def is present in the game. Otherwise falls back to giving the vanilla Cannibal trait.
    /// Does nothing if the pawn already has the gene/trait.
    /// </summary>
    public class IngestionOutcomeDoer_RHF_GiveCannibalTrait : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            GeneDef cannibalGene = DefDatabase<GeneDef>.GetNamedSilentFail("Cannibal");
            if (cannibalGene != null && pawn.genes != null)
            {
                if (!pawn.genes.HasXenogene(cannibalGene) && !pawn.genes.HasEndogene(cannibalGene))
                    pawn.genes.AddGene(cannibalGene, xenogene: true);
                return;
            }

            TraitDef cannibalTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Cannibal");
            if (cannibalTrait != null && pawn.story?.traits != null && !pawn.story.traits.HasTrait(cannibalTrait))
                pawn.story.traits.GainTrait(new Trait(cannibalTrait));
        }
    }
}
