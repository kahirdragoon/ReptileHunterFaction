# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

DO NOT GUESS, ASK IF UNSURE

## Project Overview

**Pawn Hunters** is a RimWorld 1.6 mod by kahirdragoon that adds a hostile alien faction (Pawn Hunters) who raid colonies to kidnap pawns and extract biological material to produce "Bloodprime" (BP). The mod is a mix of XML definitions, Harmony patches, and custom C# game logic compiled into `Assemblies/PawnHunters.dll`.

The original already decompiled Rimworld Source can be found under D:\Rimworld Modding\Rimworld Source

## Build & Development

**Solution**: `PawnHunters.sln` (Visual Studio)

To build: open the solution in Visual Studio, or run `dotnet build` from `Source/PawnHunters/`. The compiled DLL lands in `1.6/Assemblies/PawnHunters.dll`. RimWorld must be installed at its default Steam path for the project references to resolve.

BUILD the dll at the end of every task to verify if there are any errors.

There are no automated tests — verification is done by loading the mod in-game. Debug actions are registered in `Source/PawnHunters/RaidSystem/KidnappingRaid/DebugActions_PH.cs` and appear under the in-game dev mode debug action menu.

Always check if something can be done with vanilla machanics and behaviour. If it can use it. Dont reinvent the wheel.

Performance is important. Cache when it makes sense. Be very careful with everything that executes per tick.

## Architecture

### Three-Tier Raid System

The mod uses three distinct raid types with parallel but separate class hierarchies:

**Small Raid** (`KidnappingRaid`) — targeted, fixed-force kidnapping:
- Force = (free colonist count − 1), ignores storyteller points
- Triggers when colony has 3–6 free colonists
- One designated kidnapper tries to grab a downed pawn and flee
- Files: `IncidentWorker_PH_KidnappingRaid` → `RaidStrategyWorker` → `LordJob_PH_KidnappingRaid` → `LordToil_PH_Assault` → `JobGiver_PH_KidnapDowned` → `JobDriver_PH_KidnapAndFlee`

**Large Raid** (`KidnappingRaidBig`) — point-based assault with corpse carrying:
- Force is storyteller-point-scaled; no colonist cap
- Carries downed/dead pawns off-map
- Files: same pattern but `*Big` variants + `LordToil_PH_RetreatWithCarry` and `JobDriver_PH_CarryCorpseOffMap`

**Boss Raid** (`KidnappingRaidBoss`) — extends Large Raid with a minimum colonist gate:
- Only fires when free colonist count ≥ `minPawnsForBossRaid` (mod setting)
- Uses `PH_KidnappingRaidStrategy_Boss`; otherwise identical to Large Raid flow
- File: `IncidentWorker_PH_KidnappingRaidBoss` (subclasses `IncidentWorker_PH_KidnappingRaidBig`)

All three share `IKidnappingLordJob.cs` interface and the pawn-targeting logic in `PHPawnTargetingUtility.cs`.

### Pawn Targeting & Mod Settings

`PHModSettings.cs` stores player-configured targeting criteria (xenotype filters, gene filters, match mode). `PHPawnTargetingUtility.cs` caches validity checks against those criteria. The mod settings UI is built in `PawnHuntersMod.cs`.

### Gene System

The faction uses a custom spawning extension (`SpawnGenesExtension.cs`) hooked via `Patch_PawnGenerator_GeneratePawn_Genes.cs`. Each pawn kind can define genes with per-gene spawn probabilities and max-count limits. This is separate from vanilla Biotech gene logic.

### Settlement Generation

`GenStep_PHSettlement.cs` drives procedural settlement layout using RimWorld's `BaseGen` symbol resolver stack:
- `BaseGen_PHGlobalSettings.cs` controls how many prisons/extraction rooms/druglabs to place
- Eight `SymbolResolver_*.cs` files handle specific room types (prison interior, extraction room, druglab, autocannon defense, mine defense)
- `SettlementGeneration_Patches.xml` also injects these room types into vanilla settlement generation via `PatchOperationAdd`
- `Patch_Settlement_MapGeneratorDef.cs` overrides the map generator for PH-owned settlements

### Drug Production Chain

`Bloodprime (BP)` requires:
1. **Drug Medicine** (plant-derived herbal ingredient)
2. **Biological Extract** (`PH_BiologicalExtract`) — extracted from any gene-targeted pawn via the `Building_Extractor` (3×2, 500W, ultra tech)
3. Luciferium + Neutroamine as vanilla ingredients
4. Crafted at a Drug Lab after research unlock

`Building_Extractor.cs` is the most complex single file (~15KB): it draws held pawns, manages extraction state via `ExtractorState.cs`, and validates pawn eligibility against the targeting criteria.

### Skull/Trophy System

When an PH raider kills a player pawn, `Patch_Pawn_Kill_PH.cs` notifies `LordJob_PH_KidnappingRaid`, which designates that raider as a skull extractor. `JobGiver_PH_ExtractSkull.cs` then gives the vanilla `ExtractSkull` job (adding an `ExtractSkull` designation so the vanilla driver doesn't abort). Collected skulls and corpses are stored in `WorldComp_SpoilsOfBattle` (`WorldComp_PHSkulls.cs`) and referenced for future  settlement map generation to place Skull/Skullspike props.

### Quest Flow

When a pawn is kidnapped: `QuestNode_GetKidnappedPlayerPawn` fires → generates an `PH_OpportunitySite_KidnappedPawnPrison` site → `SitePartWorker_PHPrison` + `GenStep_PHPrison` build a prison map. Quest timeout is 12–28 hours; missing it sends the pawn to "further processing".

### Complex Looting

When PH raids an ancient complex or site with crates, a separate lord job handles systematic looting:
- Raiders are assigned rooms; each explores and loots crates via `JobGiver_PH_LootCrate` / `JobDriver_PH_OpenAndLoot`
- `MapComponent_PH_ComplexWatch` monitors the map and fires `ThreatAwakened` / `AllCratesDone` memos to trigger retreat
- `Patch_Map_FinalizeInit_PH.cs` installs the component on relevant maps
- Files: `LordJob_PH_ComplexLooting`, `LordToil_PH_ComplexLoot`, `MapComponent_PH_ComplexWatch`

### Other Harmony Patches

- `Patch_ChooseOrGenerateIdeo.cs` — postfix on `IdeoGenerator.GenerateIdeo`; forces Barbarian, PSECannibal, and GM_CannibalStyle themes onto generated PH ideologies (gracefully skips styles not loaded)
- `Patch_GlobalSetting_Clear.cs` — hooks `GlobalSettings.Clear()` to also reset `BaseGen_PHGlobalSettings`
- `Patch_PawnGenerator_RemoveSBDFromHuners.cs` — strips BP items from PH pawns at generation so raiders don't spawn carrying the drug
- `Patch_FactionGiftUtility_PH.cs` / `Patch_GiftAcceptance_PH.cs` — handle prisoner gifting/trading to the PH faction; qualifying prisoners (matching targeting criteria) reduce raid size or provide other benefits

### Optional Mod Compatibility

`Mods/` contains patches guarded by `<mods>` tags:
- **Lamia**: adds 8 reptilian xenotypes to PH faction pawn generation
- **Odyssey**: adds vacuum resistance to BP

### XML Patches (`Patches/`)

Beyond `SettlementGeneration_Patches.xml`, two vanilla-compatibility patches run unconditionally:
- `ApparelArmorImplant_Patches.xml` — adds `SpacerOrUltraTechApparelOrArmor` tag to spacer/ultra apparel defs that lack it
- `Hediff_Patches.xml` — adds `CombatDetrimental` tag to a set of vanilla hediffs (Joywire, DrillArm, etc.)

### Key Static Reference Files

- `PawnHuntersDefOf.cs` — mod def references
- `VanillaDefOf.cs` / `VanillaTerrainDefOf.cs` — vanilla def references used across the mod
- `Languages/English/Keyed/PH_Keyed.xml` — all user-facing localization strings

## XML Def Naming Convention

All mod-specific defs use the `PH_` prefix (e.g., `PH_PawnHunters`, `PH_KidnappingRaid`, `PH_BP`). C# class names also follow `*_PH_*` or `PH*` patterns.

## C# Conventions
- Use C# 14 features (`LangVersion>latest` is set)
- Target framework is `net481` (Mono/.NET Framework 4.8.1) — C# 14 syntax compiles fine, but .NET 5+ runtime APIs do not exist; rely only on APIs available in .NET Framework 4.8