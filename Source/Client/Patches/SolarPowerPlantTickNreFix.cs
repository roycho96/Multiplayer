using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Patches
{
    /// <summary>
    /// Finalizer for CompPowerPlantSolar.get_DesiredPowerOutput — catches NRE
    /// thrown when the solar generator's parent thing has a null Map (e.g.
    /// during gravship transit / partial despawn).
    ///
    /// Root cause: CompPowerPlantSolar.get_DesiredPowerOutput calls
    /// Map.skyManager (via SkyManager.CurSkyGlow) while Map can be null.
    /// CompPowerPlant.CompTick calls get_DesiredPowerOutput, and Thing.DoTick
    /// is wrapped by SeededPushPrefixThing (Rand.PushState).  Harmony skips
    /// postfixes when an exception propagates, so PopStatePostfixThing never
    /// runs — Rand stack leaks +1 per NRE, producing "Random state stack is
    /// not empty" log spam (Desync-47, Desync-58) and per-client Rand
    /// correction-order divergence → desync.
    ///
    /// We patch get_DesiredPowerOutput rather than CompTick because:
    ///   - The NRE originates there, not in the surrounding tick logic.
    ///   - Patching the narrowest method limits blast radius.
    ///   - The same getter is called by subclasses (MUS_SpaceBase_SolarGeneratorModule
    ///     extends CompPowerPlantSolar) so one patch covers all inheritors.
    /// </summary>
    [HarmonyPatch(typeof(CompPowerPlantSolar), "get_DesiredPowerOutput")]
    public static class SolarPowerPlantTickNreFix
    {
        static Exception Finalizer(Exception __exception, CompPowerPlantSolar __instance)
        {
            if (__exception is NullReferenceException && __instance?.parent?.Map == null)
            {
                // Swallow NRE caused by null Map — the solar panel is despawned /
                // mid-gravship-transit.  Returning null tells Harmony to suppress
                // the exception so the normal PopStatePostfixThing postfix can run
                // and keep the Rand stack balanced.
                Log.WarningOnce(
                    $"[Multiplayer] Suppressed NRE in CompPowerPlantSolar.get_DesiredPowerOutput for {__instance?.parent} (Map is null — despawning/gravship). Rand stack protected.",
                    (__instance?.parent?.thingIDNumber ?? 0) ^ 0x5C3D8A4);
                return null;
            }

            return __exception;
        }
    }
}
