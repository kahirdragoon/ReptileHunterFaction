# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Reptile Hunter Faction** is a RimWorld 1.6 mod by kahirdragoon that adds a hostile alien faction (Reptile Hunters) who raid colonies to kidnap pawns and extract biological material to produce the "Super Bug Drug" (SBD). The mod is a mix of XML definitions, Harmony patches, and custom C# game logic compiled into `Assemblies/ReptileHunterFaction.dll`.

## Build & Development

**Solution**: `ReptileHunterFaction.sln` (Visual Studio)

To build: open the solution in Visual Studio (or run `dotnet build`). The compiled DLL should land in `Assemblies/ReptileHunterFaction.dll`. RimWorld must be installed at its default Steam path for the project references to resolve.

There are no automated tests — verification is done by loading the mod in-game. Debug actions are registered in `Source/ReptileHunterFaction/RaidSystem/KidnappingRaid/DebugActions_RHF.cs` and appear under the in-game dev mode debug action menu.

## Architecture

### Two-Tier Raid System

The mod uses two distinct raid types with parallel but separate class hierarchies:

**Small Raid** (`KidnappingRaid`) — targeted, fixed-force kidnapping:
- Force = (free colonist count − 1), ignores storyteller points
- Triggers when colony has 3–6 free colonists
- One designated kidnapper tries to grab a downed pawn and flee
- Files: `IncidentWorker_RHF_KidnappingRaid` → `RaidStrategyWorker` → `LordJob_RHF_KidnappingRaid` → `LordToil_RHF_Assault` → `JobGiver_RHF_KidnapDowned` → `JobDriver_RHF_KidnapAndFlee`

**Large Raid** (`KidnappingRaidBig`) — point-based assault with corpse carrying:
- Force is storyteller-point-scaled; no colonist cap
- Carries downed/dead pawns off-map
- Files: same pattern but `*Big` variants + `LordToil_RHF_RetreatWithCarry` and `JobDriver_RHF_CarryCorpseOffMap`

Both share `IKidnappingLordJob.cs` interface and the pawn-targeting logic in `RHFPawnTargetingUtility.cs`.

### Pawn Targeting & Mod Settings

`RHFModSettings.cs` stores player-configured targeting criteria (xenotype filters, gene filters, match mode). `RHFPawnTargetingUtility.cs` caches validity checks against those criteria. The mod settings UI is built in `ReptileHunterFactionMod.cs`.

### Gene System

The faction uses a custom spawning extension (`SpawnGenesExtension.cs`) hooked via `Patch_PawnGenerator_GeneratePawn_Genes.cs`. Each pawn kind can define genes with per-gene spawn probabilities and max-count limits. This is separate from vanilla Biotech gene logic.

### Settlement Generation

`GenStep_RHFSettlement.cs` drives procedural settlement layout using RimWorld's `BaseGen` symbol resolver stack:
- `BaseGen_RHFGlobalSettings.cs` controls how many prisons/extraction rooms/druglabs to place
- Eight `SymbolResolver_*.cs` files handle specific room types (prison interior, extraction room, druglab, autocannon defense, mine defense)
- `SettlementGeneration_Patches.xml` also injects these room types into vanilla settlement generation via `PatchOperationAdd`
- `Patch_Settlement_MapGeneratorDef.cs` overrides the map generator for RHF-owned settlements

### Drug Production Chain

`SBD (Super Bug Drug)` requires:
1. **Drug Medicine** (plant-derived herbal ingredient)
2. **Concentrated Insectoid Blood** — extracted from humanoid insectoids via the `Building_Extractor` (3×2, 500W, ultra tech)
3. Luciferium + Neutroamine as vanilla ingredients
4. Crafted at a Drug Lab after research unlock

`Building_Extractor.cs` is the most complex single file (~15KB): it draws held pawns, manages extraction state via `ExtractorState.cs`, and validates pawn eligibility against the targeting criteria.

### Skull/Trophy System

When an RHF raider kills a player pawn, `Patch_Pawn_Kill_RHF.cs` notifies `LordJob_RHF_KidnappingRaid`, which designates that raider as a skull extractor. `JobGiver_RHF_ExtractSkull.cs` then gives the vanilla `ExtractSkull` job (adding an `ExtractSkull` designation so the vanilla driver doesn't abort). Collected skulls and corpses are stored in `WorldComp_SpoilsOfBattle` (`WorldComp_RHFSkulls.cs`) and referenced for future  settlement map generation to place Skull/Skullspike props.

### Quest Flow

When a pawn is kidnapped: `QuestNode_GetKidnappedPlayerPawn` fires → generates an `RHF_OpportunitySite_KidnappedPawnPrison` site → `SitePartWorker_RHFPrison` + `GenStep_RHFPrison` build a prison map. Quest timeout is 12–28 hours; missing it sends the pawn to "further processing".

### Other Harmony Patches

- `Patch_ChooseOrGenerateIdeo.cs` — postfix on `IdeoGenerator.GenerateIdeo`; forces Barbarian, PSECannibal, and GM_CannibalStyle themes onto generated RHF ideologies (gracefully skips styles not loaded)
- `Patch_GlobalSetting_Clear.cs` — hooks `GlobalSettings.Clear()` to also reset `BaseGen_RHFGlobalSettings`
- `Patch_PawnGenerator_RemoveSBDFromHuners.cs` — strips SBD items from RHF pawns at generation so raiders don't spawn carrying the drug

### Optional Mod Compatibility

`Mods/` contains patches guarded by `<mods>` tags:
- **Lamia**: adds 8 reptilian xenotypes to RHF faction pawn generation
- **Odyssey**: adds vacuum resistance to SBD

### XML Patches (`Patches/`)

Beyond `SettlementGeneration_Patches.xml`, two vanilla-compatibility patches run unconditionally:
- `ApparelArmorImplant_Patches.xml` — adds `SpacerOrUltraTechApparelOrArmor` tag to spacer/ultra apparel defs that lack it
- `Hediff_Patches.xml` — adds `CombatDetrimental` tag to a set of vanilla hediffs (Joywire, DrillArm, etc.)

### Key Static Reference Files

- `ReptileHunterFactionDefOf.cs` — mod def references
- `VanillaDefOf.cs` / `VanillaTerrainDefOf.cs` — vanilla def references used across the mod
- `Languages/English/Keyed/RHF_Keyed.xml` — all user-facing localization strings

## XML Def Naming Convention

All mod-specific defs use the `RHF_` prefix (e.g., `RHF_ReptileHunters`, `RHF_KidnappingRaid`, `RHF_SBD`). C# class names also follow `*_RHF_*` or `RHF*` patterns.
