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

There are no automated tests — verification is done by loading the mod in-game. Debug actions are registered in `Source/PawnHunters/RaidSystem/DebugActions_PH.cs` and appear under the in-game dev mode debug action menu.

Always check if something can be done with vanilla machanics and behaviour. If it can use it. Dont reinvent the wheel.

Performance is important. Cache when it makes sense. Be very careful with everything that executes per tick.

## Architecture

### Three-Tier Raid System

The mod uses three distinct raid types with parallel but separate class hierarchies:

**Small Raid** (`KidnappingRaid`) — targeted, fixed-force kidnapping:
- Force = floor((adult free colonists + adult slaves) / 2) − raid discount (from `WorldComp_SpoilsOfBattle`); storyteller points only pick the Combat group maker (tier mix), not the raider count. The group is picked by strict bracket (lowest `maxTotalPoints` ≥ points, so each group covers previous max < points ≤ own max), unlike vanilla where every group with a higher max is eligible
- Triggers when adult free colonists + adult slaves ≥ `minColonistsForKidnappingRaid` (mod setting, default 3), the force before the discount is at least 1, and at least `minQualifyingPawns` targetable pawns exist; no upper colonist cap. The incident def's `minThreatPoints` is 500
- The count helper `IncidentWorker_PH_KidnappingRaid.CountAdultColonistsAndSlaves` is shared by the fire gate and the raid sizing (`RaidSize`). `mapPawns.FreeColonistsSpawned` (and the `FreeAdult*` variants) already include slaves — a slave's `HostFaction` is null — so never add `SlavesOfColonySpawned` on top
- If the discount covers the whole force, `TryExecuteWorker` fires the raid as "paid off": it spends just enough prisoners to cover it, shows a message and sends nobody. `SpawnThreats` must never return null — vanilla then generates a full points-based raid instead
- `WorldComp_SpoilsOfBattle.ConsumeRaidDiscount(maxRaiders)` only spends what a raid actually uses (2 qualifying prisoners per raider); leftover prisoners, including an odd one, carry over to the next raid. The big/boss raids share `RaidStrategyWorker_PH_KidnappingRaidBig.ApplyGiftDiscount` (250 points per raider, never below `minPawns` raiders' worth)
- One designated kidnapper tries to grab a downed pawn and flee
- Files: `IncidentWorker_PH_KidnappingRaid` → `RaidStrategyWorker_PH_KidnappingRaid` → `LordJob_PH_KidnappingRaid` → `LordToil_PH_Assault` → `JobGiver_PH_KidnapDowned` → `JobDriver_PH_KidnapAndFlee`

**Large Raid** (`KidnappingRaidBig`) — point-based assault with corpse carrying:
- Force is storyteller-point-scaled; no colonist cap
- Carries downed/dead pawns off-map
- Files: same pattern but `*Big` variants + `LordToil_PH_RetreatWithCarry` and `JobDriver_PH_CarryCorpseOffMap`

**Boss Raid** (`KidnappingRaidBoss`) — extends Large Raid with a minimum colonist gate:
- Only fires when free colonist count ≥ `minPawnsForBossRaid` (mod setting)
- Uses `PH_KidnappingRaidStrategy_Boss`; otherwise identical to Large Raid flow
- File: `IncidentWorker_PH_KidnappingRaidBoss` (subclasses `IncidentWorker_PH_KidnappingRaidBig`)

All three share `IKidnappingLordJob.cs` interface and the pawn-targeting logic in `PHPawnTargetingUtility.cs`. The lord toils' 60-tick candidate scans (downed targets, standing prisoners/colonists, player corpses, closest candidate) live in `PHRaidTargetUtility` and fill toil-owned buffers. They read `SpawnedPawnsInFaction(Faction.OfPlayer)` and `PrisonersOfColonySpawned` (maintained lists) instead of `AllPawnsSpawned`; `FreeColonistsSpawned` is rebuilt on every call, so read it once per tick, never per raider. All three `RaidStrategyWorker_PH_*` classes override `CanUseWith` so only the `PH_PawnHunters` faction can use them. PH raids and complex looters get the faction from `PHFactionUtility.RaidingFaction`, which is null once the faction is defeated or deactivated, so they stop then.

The special raid duties (kidnapper, corpse carrier, skull extractor) have a single job giver. When its assignment is over it must call `PHRaidDutyUtility.ResetDutyAndThink` (resets to the toil's free duty via `IKidnappingLordJob.ResetToFreeDuty`) instead of returning null: a duty that yields no job falls through the Humanlike think tree to its final `JobGiver_ExitMapBest`, and the raider walks off the map.

### Pawn Targeting & Mod Settings

`PHModSettings.cs` stores player-configured targeting criteria (xenotype filters, gene filters, match mode) and raid gates (`minQualifyingPawns`, `minPawnsForBossRaid`, `minColonistsForKidnappingRaid`). `PHPawnTargetingUtility.cs` caches validity checks against those criteria; the cache is built in its `[StaticConstructorOnStartup]` constructor and rebuilt in `PawnHuntersMod.WriteSettings`. Never touch the DefDatabase from the `Mod` constructor: it runs before defs are loaded. The mod settings UI is built in `PawnHuntersMod.cs`.

### Gene System

The faction uses a custom spawning extension (`SpawnGenesExtension.cs`) hooked via `Patch_PawnGenerator_GeneExtension.cs` (both in `Source/GeneSpawnerExtension/`). Each pawn kind can define genes with per-gene spawn probabilities and max-count limits. This is separate from vanilla Biotech gene logic.

### Settlement Generation

`GenStep_PHSettlement.cs` drives procedural settlement layout using RimWorld's `BaseGen` symbol resolver stack:
- `BaseGen_PHGlobalSettings.cs` controls how many prisons/extraction rooms/druglabs to place
- `SymbolResolver_*.cs` files handle specific room types and contents (prison interior, extraction room, druglab, autocannon defense, mine defense, butchery, genelab, prisoners, boss assignment, hunting hut prison)
- `SettlementGeneration_Patches.xml` also injects these room types into vanilla settlement generation via `PatchOperationAdd`
- `Patch_Settlement_MapGeneratorDef.cs` overrides the map generator for PH-owned settlements

### Drug Production Chain

`Bloodprime (BP)` requires:
1. **Drug Medicine** (plant-derived herbal ingredient)
2. **Biological Extract** (`PH_BiologicalExtract`) — extracted from any gene-targeted pawn via the `Building_Extractor` (3×2, 500W, ultra tech)
3. Luciferium + Neutroamine as vanilla ingredients
4. Crafted at a Drug Lab after research unlock

`Building_Extractor.cs` is the most complex single file (~15KB): it draws held pawns, manages extraction state via `ExtractorState.cs`, and validates pawn eligibility against the targeting criteria. Selected prisoners and downed pawns are carried in by haulers via `WorkGiver_CarryToExtractor` (`PH_CarryToExtractor`); vanilla's carry work givers only cover their own buildings.

### Skull/Trophy System

When an PH raider kills a player pawn, `Patch_Pawn_Kill_PH.cs` notifies `LordJob_PH_KidnappingRaid`, which designates that raider as a skull extractor. `JobGiver_PH_ExtractSkull.cs` then gives the vanilla `ExtractSkull` job (adding an `ExtractSkull` designation so the vanilla driver doesn't abort). The designation is player-visible, so the LordJob tracks the corpses it designated and removes stale ones (extractor lost, retreat, lord cleanup, after a load). Collected skulls and corpses are stored in `WorldComp_SpoilsOfBattle` (`WorldComp_SpoilsOfBattle.cs`) and referenced for future settlement map generation to place Skull/Skullspike props. A corpse only references its dead pawn, which `WorldPawns` saves and keeps while the corpse exists, so `AddCorpse` strips apparel, equipment and inventory. The big raid's carrier takes the corpse out of its carry container (`carryTracker.innerContainer.Remove`) because `Pawn.ExitMap` destroys any carried non-pawn. Once the PH faction is defeated the stored corpses are discarded (`Corpse.PostCorpseDestroy(pawn, discarded: true)`; a plain `Destroy` would give every stored colonist a new Ideology funeral obligation). This is triggered by `Patch_SettlementDefeat_PH` (postfix on `SettlementDefeatUtility.CheckDefeated`, where vanilla sets `Faction.defeated`) and checked once on load.

### Quest Flow

When a pawn is kidnapped: `QuestNode_GetKidnappedPlayerPawn` fires → generates an `PH_OpportunitySite_KidnappedPawnPrison` site → `SitePartWorker_PHPrison` + `GenStep_PH_HuntingHutPrisonGen` (GenStepDef `PH_KidnappedPawnPrison_VFE` in the VEF folder) + `SymbolResolver_PH_HuntingHutPrison` build a prison map. The prisoner's legs are injured with vanilla `HealthUtility.DamageLegsUntilIncapableOfMoving` (no bleeding wounds), and the traps belong to the captor faction (unowned traps count as known to every pawn with a faction). Quest timeout is 5–13 days; missing it sends the pawn to "further processing".
- `SitePartWorker_PHPrison` stores the kidnapped pawn in the deep-saved `part.things`, so it removes the pawn from `WorldPawns` first (otherwise it is saved twice). Pawns already held by a site part are skipped when picking a kidnapped pawn
- Prison guards get a `LordJob_DefendBase` (like vanilla work-site camps); pawns spawned without a Lord walk off the map

### Complex Looting

When PH raids an ancient complex or site with crates, a separate lord job handles systematic looting:
- Raiders are assigned rooms; each explores and loots crates via `JobGiver_PH_ExploreRoom` / `JobDriver_PH_ExploreRoom` (files `JobGiver_PH_LootCrate.cs` / `JobDriver_PH_OpenAndLoot.cs`)
- Rooms are keyed by their lowest cell index, not `Room.ID`: that is a runtime counter and rooms are rebuilt on load
- `JobGiver_PH_ExploreRoom` walks `regionGrid.AllRooms` (not every map cell) and checks reachability once per region, since every cell of a region is equally reachable
- `MapComponent_PH_ComplexWatch` is a `CustomMapComponent` (vanilla does not auto-add those to every map). `GenStep_PH_ComplexWatch` adds it; its `GenStepDef` in `Mods/Ideology/Defs/ComplexWatch.xml` uses `linkWithSite` = `AncientComplex`, so it only runs when a complex map is generated (caravan arrival or gravship landing). It waits 1–1.5 in-game hours, then has a 25% chance to spawn 1–2 scouts, unless the map's parent is no longer a `Site` (the tile was settled)
- Never make a plain `MapComponent` subclass that should only exist on some maps: `Map.FillComponents` adds every non-abstract `MapComponent` subclass to every map, and removing it from `map.components` inside `FinalizeInit`/`MapComponentTick` skips the next component in vanilla's index loop
- The mechanitor complex (`AncientComplex_Mechanitor`) is deliberately not linked: its layout has `roomRewardCrateFactor` 0, so it has no loot crates and no Luciferium
- `LordToil_PH_ComplexLoot` sends the `ThreatAwakened` / `AllCratesDone` memos that trigger retreat
- Files: `LordJob_PH_ComplexLooting`, `LordToil_PH_ComplexLoot`, `MapComponent_PH_ComplexWatch`

### Other Harmony Patches

- `Patch_ChooseOrGenerateIdeo.cs` — postfix on `IdeoGenerator.GenerateIdeo`; forces Barbarian, PSECannibal, and GM_CannibalStyle themes onto generated PH ideologies (gracefully skips styles not loaded)
- `Patch_GlobalSetting_Clear.cs` — hooks `GlobalSettings.Clear()` to also reset `BaseGen_PHGlobalSettings`
- `Patch_SettlementDefeat_PH.cs` — postfix on `SettlementDefeatUtility.CheckDefeated`; discards the stored raid corpses when the PH faction is defeated
- `Patch_PawnGenerator_RemoveSBDFromHuners.cs` — strips BP items from PH pawns at generation so raiders don't spawn carrying the drug
- `Patch_FactionGiftUtility_PH.cs` / `Patch_GiftAcceptance_PH.cs` — handle prisoner gifting/trading to the PH faction; qualifying prisoners (matching targeting criteria) reduce raid size or provide other benefits

### Dependencies

Biotech and Vanilla Expanded Framework are hard dependencies (declared in `About.xml`). `PawnHunters.dll` references VEF's `KCSG.dll`; without VEF, RimWorld rejects the whole assembly.

### Optional Mod Compatibility

`Mods/` contains patches guarded by `<mods>` tags:
- **Lamia**: adds 8 reptilian xenotypes to PH faction pawn generation
- **Odyssey**: adds vacuum resistance to BP
- Also present: `AlphaGenes`, `Anomaly`, `Ideology` (ancient complex scanner building), `VanillaExpandedFramework` (settlement layouts/structures), `VFE_Security` (turret replacement)

### XML Patches (`Patches/`)

Beyond `SettlementGeneration_Patches.xml`, two vanilla-compatibility patches run unconditionally:
- `ApparelArmorImplant_Patches.xml` — adds `SpacerOrUltraTechApparelOrArmor` to spacer/ultra apparel defs that lack it, both as an apparel tag (`apparel.tags`, used by the pawnkinds' `apparelTags`) and as a trade tag (`tradeTags`, used by the settlement loot filter's `tradeTagsToAllow`)
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