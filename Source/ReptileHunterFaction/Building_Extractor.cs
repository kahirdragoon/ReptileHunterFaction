using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ReptileHunterFaction;
[StaticConstructorOnStartup]
internal class Building_Extractor : Building_Enterable, IThingHolderWithDrawnPawn, IThingHolder
{
    private static GeneDef insectBloodGeneDef = DefDatabase<GeneDef>.GetNamed("AG_InsectBlood", false) ?? DefDatabase<GeneDef>.GetNamed("VRE_BugBlood", false);
    private static BodyPartDef stomachDef = DefDatabase<BodyPartDef>.GetNamed("Stomach", false);
    private int fabricationTicksLeft;
    private Effecter? effectStart;
    private Effecter? effectHusk;
    private Mote? workingMote;
    private Sustainer? sustainerWorking;
    private Effecter? progressBarEffecter;
    public static readonly Texture2D CancelLoadingIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
    public static readonly CachedTexture InsertPersonIcon = new CachedTexture("UI/Icons/InsertPersonSubcoreScanner");
    private static Dictionary<Rot4, ThingDef> MotePerRotationRip = new Dictionary<Rot4, ThingDef>()
        {
          {
            Rot4.South,
            ThingDefOf.RipScannerGlow_South
},
          {
            Rot4.East,
            ThingDefOf.RipScannerGlow_East
          },
          {
    Rot4.West,
            ThingDefOf.RipScannerGlow_West
          },
          {
    Rot4.North,
            ThingDefOf.RipScannerGlow_North
          }
        };
    private static readonly Dictionary<Rot4, Vector3> HuskEffectOffsets = new Dictionary<Rot4, Vector3>()
    {
      {
        Rot4.North,
        new Vector3(0.0f, 0.0f, 0.47f)
      },
      {
        Rot4.South,
        new Vector3(0.0f, 0.0f, -0.3f)
      },
      {
        Rot4.East,
        new Vector3(0.4f, 0.0f, -0.025f)
      },
      {
        Rot4.West,
        new Vector3(-0.4f, 0.0f, -0.025f)
      }
    };
    private const float ProgressBarOffsetZ = -0.8f;
    public CachedTexture InitScannerIcon = new CachedTexture("UI/Icons/SubcoreScannerStart");

    public float HeldPawnDrawPos_Y => DrawPos.y + 0.03658537f;

    public float HeldPawnBodyAngle => Rotation.AsAngle;

    public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

    public bool PowerOn => this.TryGetComp<CompPowerTrader>().PowerOn;

    public override Vector3 PawnDrawOffset => Vector3.zero;

    public Pawn? Occupant
    {
        get
        {
            for (int index = 0; index < innerContainer.Count; ++index)
            {
                if (innerContainer[index] is Pawn occupant)
                    return occupant;
            }
            return null;
        }
    }

    public ExtractorState State
    {
        get
        {
            return !PowerOn ? ExtractorState.Inactive : 
                Occupant == null ? ExtractorState.WaitingForOccupant :
                ExtractorState.Occupied;
        }
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        progressBarEffecter?.Cleanup();
        progressBarEffecter = null;
        effectHusk?.Cleanup();
        effectHusk = null;
        effectStart?.Cleanup();
        effectStart = null;
        if (!BeingTransportedOnGravship && Occupant != null)
            KillOccupant();
        base.DeSpawn(mode);
    }

    public override AcceptanceReport CanAcceptPawn(Pawn pawn)
    {
        if (!pawn.IsColonist && !pawn.IsSlaveOfColony && !pawn.IsPrisonerOfColony)
            return false;
        if (selectedPawn != null && selectedPawn != pawn)
            return false;
        if (!PowerOn)
            return "CannotUseNoPower".Translate();
        if (pawn.genes is null || (insectBloodGeneDef != null && !pawn.genes.HasActiveGene(insectBloodGeneDef)))
            return "ExtractorNoInsectBlood".Translate();
        if(!HasNaturalVitalOrgans(pawn))
            return "ExtractorNoRequiredOrgans".Translate();

        if (State != ExtractorState.WaitingForOccupant)
        {
            switch (State)
            {
                case ExtractorState.Inactive:
                    return "SubcoreScannerNotInit".Translate();
                case ExtractorState.Occupied:
                    return "SubcoreScannerOccupied".Translate();
            }
        }
        else
        {
            if (pawn.IsQuestLodger())
                return "CryptosleepCasketGuestsNotAllowed".Translate();
            if (pawn.DevelopmentalStage.Baby())
                return "SubcoreScannerBabyNotAllowed".Translate();
        }
        return true;
    }

    private static bool HasNaturalVitalOrgans(Pawn pawn)
    {
        if (pawn == null) return false;

        var heart = pawn.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Heart).FirstOrDefault();
        var stomach = pawn.RaceProps.body.GetPartsWithDef(stomachDef).FirstOrDefault();
        var lungs = pawn.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Lung).ToList();

        bool IsNatural(BodyPartRecord part) =>
            part != null &&
            !pawn.health.hediffSet.PartIsMissing(part) &&
            !pawn.health.hediffSet.IsBionicOrImplant(part.def);

        return IsNatural(heart) && IsNatural(stomach) && lungs.All(l => IsNatural(l));
    }

    public override void TryAcceptPawn(Pawn pawn)
    {
        if (!CanAcceptPawn(pawn))
            return;
        int num = pawn.DeSpawnOrDeselect() ? 1 : 0;
        if (pawn.holdingOwner != null)
            pawn.holdingOwner.TryTransferToContainer(pawn, innerContainer);
        else
            innerContainer.TryAdd(pawn);
        if (num != 0)
            Find.Selector.Select(pawn, false, false);
        fabricationTicksLeft = def.building.subcoreScannerTicks;
    }

    public void EjectContents()
    {
        Pawn? occupant = Occupant;
        if (occupant == null)
        {
            innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
        }
        else
        {
            KillOccupant();
            for (int index = innerContainer.Count - 1; index >= 0; --index)
            {
                if (innerContainer[index] is Pawn || innerContainer[index] is Corpse)
                    innerContainer.TryDrop(innerContainer[index], InteractionCell, Map, ThingPlaceMode.Near, 1, out Thing _);
            }
            innerContainer.ClearAndDestroyContents();
        }
        selectedPawn = null;
    }

    private void KillOccupant()
    {
        Pawn? occupant = Occupant;
        if(occupant is null)
            return;

        occupant.forceNoDeathNotification = true;

        DamageInfo dinfo = new DamageInfo(DamageDefOf.ExecutionCut, 9999f, 999f, hitPart: occupant.health.hediffSet.GetBrain());
        dinfo.SetIgnoreInstantKillProtection(true);
        dinfo.SetAllowDamagePropagation(false);
        occupant.TakeDamage(dinfo);

        var heart = occupant.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Heart).FirstOrDefault();
        var stomach = occupant.RaceProps.body.GetPartsWithDef(DefDatabase<BodyPartDef>.GetNamed("Stomach")).FirstOrDefault();
        var lungs = occupant.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Lung).ToList();

        void RemovePart(BodyPartRecord part)
        {
            if (part != null && !occupant.health.hediffSet.PartIsMissing(part))
            {
                Hediff missingPart = HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, occupant, part);
                occupant.health.AddHediff(missingPart);
            }
        }

        RemovePart(heart);
        RemovePart(stomach);
        lungs.ForEach(RemovePart);

        occupant.forceNoDeathNotification = false;
        ThoughtUtility.GiveThoughtsForPawnExecuted(occupant, null, PawnExecutionKind.OrganHarvesting);
        Messages.Message("MessagePawnKilledRipscanner".Translate(occupant.Named("PAWN")), occupant, MessageTypeDefOf.NegativeHealthEvent);
    }

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
    {
        foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(selPawn))
            yield return floatMenuOption;

        if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
        {
            yield return new FloatMenuOption(("CannotEnterBuilding".Translate(this) + ": " + "NoPath".Translate().CapitalizeFirst()), null);
        }
        else
        {
            AcceptanceReport acceptanceReport = CanAcceptPawn(selPawn);
            if (acceptanceReport.Accepted)
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("EnterBuilding".Translate(this), (Action)(() =>
                {
                    Find.WindowStack.Add((Window)Dialog_MessageBox.CreateConfirmation("ConfirmRipscanPawn".Translate(selPawn.Named("PAWN")), (() => SelectPawn(selPawn)), true));
                })), selPawn, this);
            else if (!acceptanceReport.Reason.NullOrEmpty())
                yield return new FloatMenuOption(("CannotEnterBuilding".Translate(this) + ": " + acceptanceReport.Reason.CapitalizeFirst()), null);
        }
    }

    public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
    {
        base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        Occupant?.Drawer.renderer.DynamicDrawPhaseAt(phase, drawLoc, neverAimWeapon: true);
    }

    protected override void Tick()
    {
        base.Tick();
        if (State == ExtractorState.Occupied)
        {
            --fabricationTicksLeft;
            if (fabricationTicksLeft <= 0)
            {
                EjectContents();
                GenPlace.TryPlaceThing(ThingMaker.MakeThing(def.building.subcoreScannerOutputDef), InteractionCell, Map, ThingPlaceMode.Near);
                if (def.building.subcoreScannerComplete != null)
                    def.building.subcoreScannerComplete.PlayOneShot(this);
            }
            if (workingMote == null || workingMote.Destroyed)
                workingMote = MoteMaker.MakeAttachedOverlay(this, Building_Extractor.MotePerRotationRip[Rotation], Vector3.zero);
            workingMote?.Maintain();
            if (effectHusk == null)
                effectHusk = EffecterDefOf.RipScannerHeadGlow.Spawn(this, MapHeld, Building_Extractor.HuskEffectOffsets[Rotation]);
            effectHusk.EffectTick((TargetInfo)(Thing)this, (TargetInfo)(Thing)this);
            if (progressBarEffecter == null)
                progressBarEffecter = EffecterDefOf.ProgressBar.Spawn();
            progressBarEffecter.EffectTick(this, TargetInfo.Invalid);
            MoteProgressBar mote = ((SubEffecter_ProgressBar)progressBarEffecter.children[0]).mote;
            mote.progress = (float)(1.0 - (double)fabricationTicksLeft / (double)def.building.subcoreScannerTicks);
            mote.offsetZ = -0.8f;
            if (def.building.subcoreScannerWorking != null)
            {
                if (sustainerWorking == null || sustainerWorking.Ended)
                    sustainerWorking = def.building.subcoreScannerWorking.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
                else
                    sustainerWorking.Maintain();
            }

            if (def.building.subcoreScannerStartEffect == null)
                return;
            if (effectStart == null)
            {
                effectStart = def.building.subcoreScannerStartEffect.Spawn();
                effectStart.Trigger(this, new TargetInfo(InteractionCell, Map));
            }
            effectStart.EffectTick(this, new TargetInfo(InteractionCell, Map));
        }
        else
        {
            effectHusk?.Cleanup();
            effectHusk = null;
            progressBarEffecter?.Cleanup();
            progressBarEffecter = null;

            effectStart?.Cleanup();
            effectStart = null;
        }
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
            yield return gizmo;

        if (SelectedPawn == null)
        {
            Command_Action commandAction = new Command_Action();
            commandAction.defaultLabel = ("InsertPerson".Translate() + "...");
            commandAction.defaultDesc = "InsertPersonSubcoreScannerDesc".Translate((NamedArgument)def.label);
            commandAction.icon = (Texture)Building_SubcoreScanner.InsertPersonIcon.Texture;
            // ISSUE: reference to a compiler-generated method
            commandAction.action = new Action(ShowInsertPawnMenu);
            if (!PowerOn)
                commandAction.Disable("NoPower".Translate().CapitalizeFirst());
            yield return (Gizmo)commandAction;
        }
        if (DebugSettings.ShowDevGizmos)
        {
            if (State == ExtractorState.Occupied)
            {
                Command_Action commandAction = new Command_Action();
                commandAction.defaultLabel = "DEV: Complete";
                // ISSUE: reference to a compiler-generated method
                commandAction.action = new Action(() => { fabricationTicksLeft = 0; });
                yield return (Gizmo)commandAction;
            }
        }
    }


    private void ShowInsertPawnMenu()
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>();

        foreach (Pawn pawn in Map.mapPawns.AllHumanlikeSpawned)
        {
            AcceptanceReport report = CanAcceptPawn(pawn);

            if (!report.Accepted)
            {
                string reason = !report.Reason.NullOrEmpty()
                    ? $"{pawn.LabelShortCap}: {report.Reason}"
                    : pawn.LabelShortCap;
                options.Add(new FloatMenuOption(reason, null));
            }
            else
            {
                options.Add(new FloatMenuOption(pawn.LabelShortCap, () =>
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "ConfirmRipscanPawn".Translate(pawn.Named("PAWN")),
                        () => SelectPawn(pawn),
                        true));
                }));
            }
        }

        // If no valid pawns, show fallback option
        if (!options.Any())
        {
            options.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));
        }

        // Show the menu
        Find.WindowStack.Add(new FloatMenu(options));
    }


    public override string GetInspectString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(base.GetInspectString());
        switch (State)
        {
            case ExtractorState.WaitingForOccupant:
                sb.AppendLineIfNotEmpty();
                sb.Append("SubcoreScannerWaitingForOccupant".Translate());
                break;
            case ExtractorState.Occupied:
                sb.AppendLineIfNotEmpty();
                sb.Append(("SubcoreScannerCompletesIn".Translate() + ": " + fabricationTicksLeft.ToStringTicksToPeriod()));
                break;
        }
        return sb.ToString();
    }

   public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look<int>(ref fabricationTicksLeft, "fabricationTicksLeft");
    }
}

