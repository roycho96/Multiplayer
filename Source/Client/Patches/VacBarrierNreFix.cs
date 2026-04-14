using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Patches
{
    /// <summary>
    /// Finalizer for Building_Door.Tick — catches NRE from null Map access
    /// on VacBarrier (Building_SupportedDoor) and MUS_SpaceBase_Door
    /// (Building_MultiTileDoor) which both inherit Building_Door.
    ///
    /// Root cause: Building_Door.Tick calls GridsUtility.GetThingList(cell, Map)
    /// while Map is null during despawning.  Thing.DoTick is wrapped by
    /// SeededPushPrefixThing (Rand.PushState); Harmony skips postfixes on
    /// exception, so PopStatePostfixThing never runs — Rand stack leaks +1
    /// per NRE.  This finalizer swallows the NRE so the normal postfix can
    /// still execute and the stack stays balanced.
    /// </summary>
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.Tick))]
    public static class BuildingDoorTickNreFix
    {
        static Exception Finalizer(Exception __exception, Building_Door __instance)
        {
            if (__exception is NullReferenceException && __instance.Map == null)
            {
                // Swallow NRE caused by null Map — the door/barrier is despawning.
                Log.WarningOnce(
                    $"[Multiplayer] Suppressed NRE in Building_Door.Tick for {__instance} (Map is null — despawning). Rand stack protected.",
                    __instance.thingIDNumber ^ 0x3A7F1C2);
                return null;
            }

            return __exception;
        }
    }
}
