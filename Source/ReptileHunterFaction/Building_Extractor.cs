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
    private static readonly BodyPartDef stomachDef = DefDatabase<BodyPartDef>.GetNamed("Stomach", false);
    private static readonly BodyPartDef torsoDef = DefDatabase<BodyPartDef>.GetNamed("Torso", false);

    private int fabricationTicksLeft;
    private Effecter? effectStart;
    private Effecter? effectHusk;
    private Mote? workingMote;
    private Sustainer? sustainerWorking;
    private Effecter? progressBarEffecter;

    private bool milestone00Applied;
    private bool milestone30Applied;
    private bool milestone60Applied;

    // Cap blood loss just below the 0.8 death threshold
    private const float MaxBloodLossSeverity = 0.74f;

    public static readonly Texture2D CancelLoadingIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
    public static readonly CachedTexture InsertPersonIcon = new("UI/Icons/InsertPersonSubcoreScanner");
    private static Dictionary<Rot4, ThingDef> MotePerRotationRip = new()
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
        if (!RHFPawnTargetingUtility.IsTargetPawn(pawn))
            return "ExtractorNoInsectBlood".Translate();
        if (!HasNaturalVitalOrgans(pawn))
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
        milestone00Applied = false;
        milestone30Applied = false;
        milestone60Applied = false;
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
            for (int index = innerContainer.Count - 1; index >= 0; --index)
            {
                if (innerContainer[index] is Pawn || innerContainer[index] is Corpse)
                    innerContainer.TryDrop(innerContainer[index], InteractionCell, Map, ThingPlaceMode.Near, 1, out Thing _);
            }
            innerContainer.ClearAndDestroyContents();
        }
        milestone00Applied = false;
        milestone30Applied = false;
        milestone60Applied = false;
        selectedPawn = null;
    }

    // Used only when the building is despawned while occupied.
    private void KillOccupant()
    {
        Pawn? occupant = Occupant;
        if (occupant is null || occupant.Dead)
            return;
        ApplyFinalExtraction(occupant);
    }

    // Removes heart and all remaining lungs — kills the pawn naturally via missing heart.
    private static void ApplyFinalExtraction(Pawn pawn)
    {
        pawn.forceNoDeathNotification = true;

        ApplyExtractionCut(pawn);
        ApplyExtractionCut(pawn);

        var heart = pawn.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Heart)
            .FirstOrDefault(h => !pawn.health.hediffSet.PartIsMissing(h));
        if (heart != null)
            pawn.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, heart));

        foreach (var lung in pawn.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Lung)
            .Where(l => !pawn.health.hediffSet.PartIsMissing(l)).ToList())
        {
            pawn.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, lung));
        }

        pawn.forceNoDeathNotification = false;

        // Fallback in case the organ removals didn't trigger death (e.g. unusual race).
        if (!pawn.Dead)
        {
            DamageInfo dinfo = new(DamageDefOf.ExecutionCut, 9999f, 999f, hitPart: pawn.health.hediffSet.GetBrain());
            dinfo.SetIgnoreInstantKillProtection(true);
            dinfo.SetAllowDamagePropagation(false);
            pawn.TakeDamage(dinfo);
        }

        ThoughtUtility.GiveThoughtsForPawnExecuted(pawn, null, PawnExecutionKind.OrganHarvesting);
        Messages.Message("MessagePawnKilledRipscanner".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NegativeHealthEvent);
    }

    // 30% milestone: remove one lung.
    private static void RemoveOneLung(Pawn pawn)
    {
        var lung = pawn.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Lung)
            .FirstOrDefault(l => !pawn.health.hediffSet.PartIsMissing(l));
        if (lung == null) return;
        pawn.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, lung));
    }

    // 60% milestone: remove stomach.
    private static void RemoveStomach(Pawn pawn)
    {
        var stomach = pawn.RaceProps.body.GetPartsWithDef(stomachDef)
            .FirstOrDefault(s => !pawn.health.hediffSet.PartIsMissing(s));
        if (stomach == null) return;
        pawn.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, stomach));
    }

    // Periodic shallow cut to the torso to drive blood loss accumulation.
    // Adds the injury hediff directly to bypass TakeDamage and its wound sounds/effects.
    private static void ApplyExtractionCut(Pawn pawn)
    {
        if (torsoDef == null) return;
        var torso = pawn.RaceProps.body.GetPartsWithDef(torsoDef).FirstOrDefault();
        if (torso == null || pawn.health.hediffSet.PartIsMissing(torso)) return;
        var injury = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, torso);
        injury.Severity = Rand.Range(1f, 3f);
        pawn.health.AddHediff(injury);
    }

    // Keep blood loss in the "Extreme" range (0.6–0.74) — debilitating but not lethal.
    private static void CapBloodLoss(Pawn pawn)
    {
        var bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        if (bloodLoss != null && bloodLoss.Severity > MaxBloodLossSeverity)
            bloodLoss.Severity = MaxBloodLossSeverity;
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
            Pawn? occupant = Occupant;

            // Eject immediately if the occupant died during extraction
            if (occupant != null && occupant.Dead)
            {
                EjectContents();
                return;
            }

            if (occupant != null && !occupant.Dead)
            {
                float progress = 1f - (float)fabricationTicksLeft / def.building.subcoreScannerTicks;

                if (!milestone00Applied && progress >= 0.05f)
                {
                    milestone00Applied = true;
                    ApplyExtractionCut(occupant);
                    ApplyExtractionCut(occupant);
                }

                if (!milestone30Applied && progress >= 0.3f)
                {
                    ApplyExtractionCut(occupant);
                    ApplyExtractionCut(occupant);
                    milestone30Applied = true;
                    RemoveOneLung(occupant);
                }

                if (!milestone60Applied && progress >= 0.6f)
                {
                    ApplyExtractionCut(occupant);
                    ApplyExtractionCut(occupant);
                    milestone60Applied = true;
                    RemoveStomach(occupant);
                }

                CapBloodLoss(occupant);
            }

            --fabricationTicksLeft;
            if (fabricationTicksLeft <= 0)
            {
                if(occupant != null && !occupant.Dead)
                    ApplyFinalExtraction(occupant);
                EjectContents();
                GenPlace.TryPlaceThing(ThingMaker.MakeThing(def.building.subcoreScannerOutputDef), InteractionCell, Map, ThingPlaceMode.Near);
                def.building.subcoreScannerComplete?.PlayOneShot(this);
            }
            if (workingMote == null || workingMote.Destroyed)
                workingMote = MoteMaker.MakeAttachedOverlay(this, MotePerRotationRip[Rotation], Vector3.zero);
            workingMote?.Maintain();
            effectHusk ??= EffecterDefOf.RipScannerHeadGlow.Spawn(this, MapHeld, HuskEffectOffsets[Rotation]);
            effectHusk.EffectTick((TargetInfo)this, (TargetInfo)this);
            progressBarEffecter ??= EffecterDefOf.ProgressBar.Spawn();
            progressBarEffecter.EffectTick(this, TargetInfo.Invalid);
            MoteProgressBar mote = ((SubEffecter_ProgressBar)progressBarEffecter.children[0]).mote;
            mote.progress = (float)fabricationTicksLeft / def.building.subcoreScannerTicks;
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
            // Free the extractor if the selected pawn is no longer actively pathing here
            // (covers drafted, downed, dead, carried, or any other job interruption)
            if (selectedPawn != null)
            {
                bool stillComing = !selectedPawn.Dead &&
                                   !selectedPawn.Downed &&
                                   selectedPawn.CurJob?.def == JobDefOf.EnterBuilding &&
                                   selectedPawn.CurJob?.targetA.Thing == this;
                if (!stillComing)
                    selectedPawn = null;
            }

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
            Command_Action commandAction = new()
            {
                defaultLabel = ("InsertPerson".Translate() + "..."),
                defaultDesc = "InsertPersonSubcoreScannerDesc".Translate((NamedArgument)def.label),
                icon = Building_SubcoreScanner.InsertPersonIcon.Texture,
                action = new Action(ShowInsertPawnMenu)
            };
            if (!PowerOn)
                commandAction.Disable("NoPower".Translate().CapitalizeFirst());
            yield return commandAction;
        }
        if (DebugSettings.ShowDevGizmos)
        {
            if (State == ExtractorState.Occupied)
            {
                Command_Action commandAction = new()

                {
                    defaultLabel = "DEV: Complete",
                    action = new Action(() => { fabricationTicksLeft = 0; })
                };
                yield return commandAction;
            }
        }
    }


    private void ShowInsertPawnMenu()
    {
        List<FloatMenuOption> options = [];

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

        if (!options.Any())
        {
            options.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }


    public override string GetInspectString()
    {
        StringBuilder sb = new();
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
        Scribe_Values.Look(ref milestone00Applied, "milestone00Applied");
        Scribe_Values.Look(ref milestone30Applied, "milestone30Applied");
        Scribe_Values.Look(ref milestone60Applied, "milestone60Applied");
    }
}

