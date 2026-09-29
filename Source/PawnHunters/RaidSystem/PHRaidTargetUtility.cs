using RimWorld;
using System.Collections.Generic;
using Verse;

namespace PawnHunters;

/// <summary>
/// Candidate scans for the raid lord toils, which run every 60 ticks. They read vanilla's maintained
/// per-faction and prisoner lists instead of AllPawnsSpawned (wildlife, visitors and the raiders themselves),
/// and fill caller-owned buffers so no lists are allocated per tick.
/// </summary>
internal static class PHRaidTargetUtility
{
    /// <summary>
    /// A downed humanlike player pawn (colonist, slave) or prisoner of the colony that matches the targeting
    /// settings and has no kidnapper yet.
    /// </summary>
    public static bool IsDownedTarget(Pawn p, IKidnappingLordJob lordJob) =>
        !p.Dead && p.Downed && p.RaceProps.Humanlike
        && (p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony)
        && !lordJob.IsTargeted(p) && PHPawnTargetingUtility.IsTargetPawn(p);

    /// <summary>A spawned corpse of a humanlike player pawn that matches the targeting settings and has no carrier yet.</summary>
    public static bool IsCorpseTarget(Corpse corpse, LordJob_PH_KidnappingRaidBig lordJob) =>
        corpse.InnerPawn is { } inner && inner.Faction == Faction.OfPlayer && inner.RaceProps.Humanlike
        && !lordJob.IsCorpseTargeted(corpse) && PHPawnTargetingUtility.IsTargetPawn(inner);

    /// <summary>Fills <paramref name="into"/> with the downed targets among player pawns and/or prisoners of the colony.</summary>
    public static void CollectDownedTargets(Map map, IKidnappingLordJob lordJob, List<Pawn> into,
        bool playerPawns = true, bool prisoners = true)
    {
        into.Clear();
        if (playerPawns) AddDownedTargets(map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer), lordJob, into);
        if (prisoners)   AddDownedTargets(map.mapPawns.PrisonersOfColonySpawned, lordJob, into);
    }

    private static void AddDownedTargets(List<Pawn> pawns, IKidnappingLordJob lordJob, List<Pawn> into)
    {
        foreach (Pawn p in pawns)
        {
            if (IsDownedTarget(p, lordJob))
                into.Add(p);
        }
    }

    /// <summary>Fills <paramref name="into"/> with the standing prisoners of the colony that match the targeting settings.</summary>
    public static void CollectStandingPrisoners(Map map, List<Pawn> into)
    {
        into.Clear();
        foreach (Pawn p in map.mapPawns.PrisonersOfColonySpawned)
        {
            if (!p.Dead && !p.Downed && p.RaceProps.Humanlike && PHPawnTargetingUtility.IsTargetPawn(p))
                into.Add(p);
        }
    }

    /// <summary>
    /// Fills <paramref name="into"/> with the standing free colonists (slaves included). FreeColonistsSpawned is
    /// rebuilt on every call, so callers take this copy once per tick instead of reading it once per raider.
    /// </summary>
    public static void CollectStandingColonists(Map map, List<Pawn> into)
    {
        into.Clear();
        foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
        {
            if (!p.Dead && !p.Downed)
                into.Add(p);
        }
    }

    /// <summary>Fills <paramref name="into"/> with the player corpses the big raid may carry off.</summary>
    public static void CollectCorpseTargets(Map map, LordJob_PH_KidnappingRaidBig lordJob, List<Corpse> into)
    {
        into.Clear();
        foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
        {
            if (t is Corpse corpse && IsCorpseTarget(corpse, lordJob))
                into.Add(corpse);
        }
    }

    /// <summary>The candidate closest to <paramref name="from"/> that is at most <paramref name="maxRange"/> tiles away, or null.</summary>
    public static T? Closest<T>(IntVec3 from, List<T> candidates, float maxRange = float.MaxValue) where T : Thing
    {
        float maxDistSq  = maxRange * maxRange; // float.MaxValue squared is +Infinity: no limit
        T?    best       = null;
        int   bestDistSq = int.MaxValue;

        foreach (T t in candidates)
        {
            int d = t.Position.DistanceToSquared(from);
            if (d <= maxDistSq && d < bestDistSq) { bestDistSq = d; best = t; }
        }
        return best;
    }
}
