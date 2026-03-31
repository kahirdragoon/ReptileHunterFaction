using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace ReptileHunterFaction;

public class GenStep_RHFPrison : GenStep
{
    private const string PrisonSitePartDefName = "RHF_KidnappedPawnPrison";
    private Faction? kidnapperFaction;
    public int count = 1;
    public bool nearMapCenter = true;
    private const int RoomWidth = 9;
    private const int RoomHeight = 6;
    private const int BedroomMaxXOffset = 2;
    private const int BedroomMaxZOffset = 4;
    private const int DividerXOffset = 4;
    private const int MineSpawnMinCount = 15;
    private const int MineSpawnMaxCount = 20;
    private const int MineSpawnPadding = 9;
    private const int MineMinSpacing = 5;

    public override int SeedPart => 84736291;

    public override void Generate(Map map, GenStepParams parms)
    {
        kidnapperFaction = map.Parent?.Faction;
        Pawn kidnappedPawn = TryGetKidnappedPawnForMap(map);

        if (kidnappedPawn == null)
        {
            Log.Warning("GenStep_RHFPrison: Could not find kidnapped pawn on site part");
            return;
        }

        // Find top-left corner for the room
        IntVec3 roomTopLeft = FindRoomLocation(map);
        if (!roomTopLeft.IsValid)
        {
            Log.Warning("GenStep_RHFPrison: Could not find valid room location");
            return;
        }

        // Build the room structure
        BuildRoomStructure(map, roomTopLeft);

        // Populate bedroom (left side)
        PopulateBedroom(map, roomTopLeft);

        // Populate prison (right side)
        PopulatePrison(map, roomTopLeft, kidnappedPawn);

        // Spawn Hunter faction guards with random count (30% = 0 guards, 60% = 1 guard, 10% = 2 guards)
        SpawnHunterGuards(map, roomTopLeft);

        // Scatter land mines around the prison.
        SpawnMinesAroundBuilding(map, roomTopLeft);
    }

    private Pawn TryGetKidnappedPawnForMap(Map map)
    {
        if (map.Parent is not Site site || site.parts == null)
            return null;

        Pawn pawn = TryTakePawnFromMatchingPart(site.parts, PrisonSitePartDefName);
        if (pawn != null)
            return pawn;

        // Fallback: find any pawn stored on any site part in case defs were patched/renamed.
        return TryTakePawnFromMatchingPart(site.parts, null);
    }

    private Pawn TryTakePawnFromMatchingPart(List<SitePart> siteParts, string? requiredDefName)
    {
        if (siteParts == null)
            return null;

        for (int i = 0; i < siteParts.Count; i++)
        {
            SitePart part = siteParts[i];
            bool defMatches = requiredDefName == null || part?.def?.defName == requiredDefName;
            if (!defMatches || part?.things == null || !part.things.Any)
                continue;

            for (int j = 0; j < part.things.Count; j++)
            {
                if (part.things[j] is Pawn pawn)
                {
                    part.things.Remove(pawn);
                    return pawn;
                }
            }
        }

        return null;
    }

    private void SpawnHunterGuards(Map map, IntVec3 roomTopLeft)
    {
        // Randomly determine guard count
        float roll = Rand.Value;
        int guardCount = roll < 0.30f ? 0 : (roll < 0.90f ? 1 : 2);

        FactionDef hunterFactionDef = ReptileHunterFactionDefOf.RHF_ReptileHunters;
        if (hunterFactionDef == null)
            return;

        Faction hunterFaction = Find.FactionManager.FirstFactionOfDef(hunterFactionDef);
        if (hunterFaction == null)
            return;

        List<IntVec3> bedroomCells = [];
        for (int x = roomTopLeft.x + 1; x <= roomTopLeft.x + BedroomMaxXOffset; x++)
        {
            for (int z = roomTopLeft.z + 1; z <= roomTopLeft.z + BedroomMaxZOffset; z++)
            {
                IntVec3 cell = new(x, roomTopLeft.y, z);
                if (cell.InBounds(map) && cell.Walkable(map) && cell.GetFirstPawn(map) == null)
                {
                    bedroomCells.Add(cell);
                }
            }
        }

        if (bedroomCells.Count == 0)
            return;

        for (int i = 0; i < guardCount; i++)
        {
            if (bedroomCells.Count == 0)
                break;

            IntVec3 spawnPos = bedroomCells.RandomElement();
            bedroomCells.Remove(spawnPos);

            // Randomly choose between ranged and melee guard
            PawnKindDef guardKind = Rand.Bool ? ReptileHunterFactionDefOf.RHF_ReptileHuntersFighter_Ranged : ReptileHunterFactionDefOf.RHF_ReptileHuntersFighter_Melee;

            // Generate a Hunter faction pawn
            PawnGenerationRequest request = new PawnGenerationRequest(
                kind: guardKind,
                faction: hunterFaction,
                tile: map.Tile
            );
            Pawn guard = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(guard, spawnPos, map);
        }
    }

    private IntVec3 FindRoomLocation(Map map)
    {
        IntVec3 center = nearMapCenter ? map.Center : CellFinder.RandomCell(map);
        
        // Try to find a clear area matching room dimensions
        for (int x = center.x - 20; x < center.x + 20; x++)
        {
            for (int z = center.z - 20; z < center.z + 20; z++)
            {
                IntVec3 candidate = new(x, map.Center.y, z);
                if (CanBuildRoom(map, candidate))
                {
                    return candidate;
                }
            }
        }

        return IntVec3.Invalid;
    }

    private bool CanBuildRoom(Map map, IntVec3 topLeft)
    {
        for (int x = topLeft.x; x < topLeft.x + RoomWidth; x++)
        {
            for (int z = topLeft.z; z < topLeft.z + RoomHeight; z++)
            {
                IntVec3 cell = new(x, topLeft.y, z);
                if (!cell.InBounds(map) || cell.GetTerrain(map).passability == Traversability.Impassable)
                    return false;
            }
        }
        return true;
    }

    private void BuildRoomStructure(Map map, IntVec3 topLeft)
    {
        int exteriorDoorX = topLeft.x + (RoomWidth / 2) - 1;
        int exteriorDoorZ = topLeft.z + RoomHeight - 1;

        // Build outer walls
        for (int x = topLeft.x; x < topLeft.x + RoomWidth; x++)
        {
            BuildWall(map, new IntVec3(x, topLeft.y, topLeft.z)); // Top wall

            IntVec3 bottomCell = new IntVec3(x, topLeft.y, topLeft.z + RoomHeight - 1);
            if (x == exteriorDoorX)
            {
                BuildDoor(map, bottomCell); // Exterior door
            }
            else
            {
                BuildWall(map, bottomCell); // Bottom wall
            }
        }

        for (int z = topLeft.z; z < topLeft.z + RoomHeight; z++)
        {
            BuildWall(map, new IntVec3(topLeft.x, topLeft.y, z)); // Left wall
            BuildWall(map, new IntVec3(topLeft.x + RoomWidth - 1, topLeft.y, z)); // Right wall
        }

        // Build internal wall dividing bedroom from prison, with an interior door
        int dividerX = topLeft.x + DividerXOffset;
        int dividerDoorZ = topLeft.z + (RoomHeight / 2);
        for (int z = topLeft.z + 1; z < topLeft.z + RoomHeight - 1; z++)
        {
            if (z == dividerDoorZ)
            {
                BuildDoor(map, new IntVec3(dividerX, topLeft.y, z));
            }
            else
            {
                BuildWall(map, new IntVec3(dividerX, topLeft.y, z));
            }
        }
    }

    private void BuildWall(Map map, IntVec3 pos)
    {
        if (!pos.InBounds(map))
            return;

        GenSpawn.Spawn(MakeThingWithDefaultStuff(ThingDefOf.Wall), pos, map);
    }

    private void BuildDoor(Map map, IntVec3 pos)
    {
        if (!pos.InBounds(map))
            return;

        GenSpawn.Spawn(MakeThingWithDefaultStuff(ThingDefOf.Door), pos, map, Rot4.West);
    }

    private void PopulateBedroom(Map map, IntVec3 roomTopLeft)
    {
        // Lay wood plank floor across bedroom interior (x+1 to x+3, z+1 to z+4)
        TerrainDef woodFloor = TerrainDefOf.WoodPlankFloor;
        for (int x = roomTopLeft.x + 1; x < roomTopLeft.x + DividerXOffset; x++)
        {
            for (int z = roomTopLeft.z + 1; z < roomTopLeft.z + RoomHeight - 1; z++)
            {
                IntVec3 cell = new(x, roomTopLeft.y, z);
                if (cell.InBounds(map))
                    map.terrainGrid.SetTerrain(cell, woodFloor);
            }
        }

        // Place 1 bed against the left wall, aligned with the divider door (z + RoomHeight/2)
        IntVec3 bedPos = new(roomTopLeft.x + 1, roomTopLeft.y, roomTopLeft.z + 3);

        GenSpawn.Spawn(MakeThingWithDefaultStuff(ThingDefOf.Bed), bedPos, map);

        // Place shelf with survival packages and human meat
        IntVec3 shelfPos = new(roomTopLeft.x + 2, roomTopLeft.y, roomTopLeft.z + 3);

        if (GenSpawn.Spawn(MakeThingWithDefaultStuff(ThingDefOf.Shelf), shelfPos, map) is Building shelf)
        {
            // Add survival packages made with human meat to shelf
            for (int i = 0; i < 3; i++)
            {
                Thing survivalPackage = ThingMaker.MakeThing(ThingDefOf.MealSurvivalPack);
                
                CompIngredients ingredientComp = survivalPackage.TryGetComp<CompIngredients>();
                ingredientComp?.RegisterIngredient(ThingDefOf.Meat_Human);
                
                GenPlace.TryPlaceThing(survivalPackage, shelfPos, map, ThingPlaceMode.Near);
            }

            // Add human meat as ingredient
            for (int i = 0; i < 4; i++)
            {
                Thing humanMeat = ThingMaker.MakeThing(ThingDefOf.Meat_Human);
                GenPlace.TryPlaceThing(humanMeat, shelfPos, map, ThingPlaceMode.Near);
            }
        }
    }

    private void PopulatePrison(Map map, IntVec3 roomTopLeft, Pawn kidnappedPawn)
    {
        int prisonX = roomTopLeft.x + 5;
        int prisonZ = roomTopLeft.z + 2;

        foreach (BodyPartRecord bodyPart in kidnappedPawn.health.hediffSet.GetNotMissingParts())
        {
            if (bodyPart.def.defName.Contains("Leg"))
            {
                kidnappedPawn.TakeDamage(new DamageInfo(DamageDefOf.Crush, 70f, hitPart: bodyPart));
            }
        }
        ApplyBiteDamageToRandomLimbs(kidnappedPawn);

        IntVec3 sleepingSpotPos = new(prisonX, roomTopLeft.y, prisonZ);
        if (GenSpawn.Spawn(MakeThingWithDefaultStuff(ThingDefOf.SleepingSpot), sleepingSpotPos, map) is Building_Bed sleepingSpot)
        {
            sleepingSpot.ForPrisoners = true;
            sleepingSpot.CompAssignableToPawn?.TryAssignPawn(kidnappedPawn);
        }

        kidnapperFaction?.kidnapped?.RemoveKidnappedPawn(kidnappedPawn);
        PawnDiedOrDownedThoughtsUtility.RemoveLostThoughts(kidnappedPawn);
        kidnappedPawn.SetFaction(null);

        if (kidnappedPawn.SpawnedOrAnyParentSpawned)
            kidnappedPawn.DeSpawnOrDeselect();

        GenSpawn.Spawn(kidnappedPawn, sleepingSpotPos, map);
        kidnappedPawn.guest.SetGuestStatus(map.ParentFaction, GuestStatus.Prisoner);
        kidnappedPawn.mindState.willJoinColonyIfRescuedInt = true;

        for (int i = 0; i < 3; i++)
        {
            IntVec3 meatPos = new(prisonX + Rand.Range(-1, 2), roomTopLeft.y, prisonZ + Rand.Range(-1, 2));
            if (meatPos.InBounds(map))
            {
                Thing meat = ThingMaker.MakeThing(ThingDefOf.Meat_Human);
                GenPlace.TryPlaceThing(meat, meatPos, map, ThingPlaceMode.Near);
            }
        }
    }

    private void ApplyBiteDamageToRandomLimbs(Pawn pawn)
    {
        // 10% chance for each limb type to be bitten off
        if (Rand.Chance(0.1f))
        {
            // Find and damage a random arm
            var arm = GetRandomBodyPartOfType(pawn, "Arm", BodyPartDepth.Outside);
            if (arm != null)
            {
                ApplyBiteDamageToLimb(pawn, arm);
            }
        }

        if (Rand.Chance(0.1f))
        {
            // Find and damage a random leg
            var leg = GetRandomBodyPartOfType(pawn, "Leg", BodyPartDepth.Outside);
            if (leg != null)
            {
                ApplyBiteDamageToLimb(pawn, leg);
            }
        }

        if (Rand.Chance(0.1f))
        {
            // Find and damage a random kidney
            var kidney = GetRandomBodyPartOfType(pawn, "Kidney", BodyPartDepth.Inside);
            if (kidney != null)
            {
                ApplyBiteDamageToLimb(pawn, kidney);
            }
        }

        if (Rand.Chance(0.1f))
        {
            // Find and damage a random lung
            var lung = GetRandomBodyPartOfType(pawn, "Lung", BodyPartDepth.Inside);
            if (lung != null)
            {
                ApplyBiteDamageToLimb(pawn, lung);
            }
        }
    }

    private BodyPartRecord? GetRandomBodyPartOfType(Pawn pawn, string partTypeName, BodyPartDepth depth)
    {
        // Get all non-missing body parts matching the specified type and depth
        List<BodyPartRecord> matchingParts = [];
        
        foreach (BodyPartRecord bodyPart in pawn.health.hediffSet.GetNotMissingParts(depth: depth))
        {
            if (bodyPart.def.defName.Contains(partTypeName))
            {
                matchingParts.Add(bodyPart);
            }
        }

        // Return a random matching part, or null if none found
        return matchingParts.Count > 0 ? matchingParts.RandomElement() : null;
    }

    private void ApplyBiteDamageToLimb(Pawn pawn, BodyPartRecord bodyPart)
    {
        if (bodyPart == null || pawn.Dead)
            return;

        // Calculate damage amount to sever the limb via overkill
        // Bite damage has 0~0.1 overkill range, so we need 10%+ overkill for guaranteed sever
        float maxPartHealth = bodyPart.def.GetMaxHealth(pawn);
        float damageAmount = maxPartHealth * 1.2f; // 120% = guaranteed sever with margin

        DamageInfo dinfo = new DamageInfo(DamageDefOf.Bite, damageAmount, 0f, -1f, null, bodyPart);
        pawn.TakeDamage(dinfo);
    }

    private Thing MakeThingWithDefaultStuff(ThingDef thingDef)
    {
        ThingDef? stuff = thingDef.MadeFromStuff ? GenStuff.DefaultStuffFor(thingDef) : null;
        Thing thing = ThingMaker.MakeThing(thingDef, stuff);
        if (kidnapperFaction != null && thingDef.CanHaveFaction)
        {
            thing.SetFaction(kidnapperFaction);
        }
        return thing;
    }

    private void SpawnMinesAroundBuilding(Map map, IntVec3 roomTopLeft)
    {
        ThingDef mineDef = DefDatabase<ThingDef>.GetNamedSilentFail("TrapIED_HighExplosive") ?? ThingDefOf.TrapSpike;
        if (mineDef == null)
            return;

        CellRect roomRect = new CellRect(roomTopLeft.x, roomTopLeft.z, RoomWidth, RoomHeight);
        int minX = roomTopLeft.x - MineSpawnPadding;
        int maxX = roomTopLeft.x + RoomWidth + MineSpawnPadding - 1;
        int minZ = roomTopLeft.z - MineSpawnPadding;
        int maxZ = roomTopLeft.z + RoomHeight + MineSpawnPadding - 1;

        int targetCount = Rand.RangeInclusive(MineSpawnMinCount, MineSpawnMaxCount);
        int placed = 0;
        int attempts = 0;
        int maxAttempts = targetCount * 20;
        List<IntVec3> placedCells = [];

        while (placed < targetCount && attempts < maxAttempts)
        {
            attempts++;
            IntVec3 cell = new IntVec3(Rand.RangeInclusive(minX, maxX), roomTopLeft.y, Rand.RangeInclusive(minZ, maxZ));
            if (!cell.InBounds(map) || roomRect.Contains(cell) || !cell.Standable(map) || cell.GetEdifice(map) != null)
                continue;

            if (cell.GetThingList(map).Count > 0)
                continue;

            if (placedCells.Any(c => c.DistanceTo(cell) < MineMinSpacing))
                continue;

            GenSpawn.Spawn(MakeThingWithDefaultStuff(mineDef), cell, map);
            placedCells.Add(cell);
            placed++;
        }
    }
}


