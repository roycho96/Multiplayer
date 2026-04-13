using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;

namespace Multiplayer.Client.Patches
{
    // Desync-51 / Desync-52 fix:
    // GravshipUtility.TryFindSpotOnGravship internally calls Region.get_RandomCell()
    // which consumes Verse.Rand.  The method is reached asymmetrically across peers
    // (host via Dialog_BeginGravshipLaunch ctor / SyncMethod replay path; client via
    // the CancelDialogBeginRitual short-circuit that never enters the ctor).
    // Wrapping the entire call with PushState / PopState isolates its Rand consumption
    // from the shared global state, so however many times each peer calls it, the
    // global Rand stream is unaffected after each call returns.
    [HarmonyPatch]
    static class GravshipFindSpotPushPop
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(typeof(GravshipUtility), "TryFindSpotOnGravship");

        // MpFirst priority so we run before any other prefix that might early-return,
        // which would cause a dangling pushed state.
        [HarmonyPriority(MpPriority.MpFirst)]
        static void Prefix(ref bool __state)
        {
            if (Multiplayer.Client == null) return;

            // Seed is the current game tick — identical on all peers because MP
            // keeps tick counts synchronised.  The exact value doesn't matter for
            // correctness; all that matters is that every peer uses the same value.
            Rand.PushState(Find.TickManager.TicksGame);
            __state = true;
        }

        // Finalizer runs even when the method throws, preventing stack imbalance.
        [HarmonyPriority(MpPriority.MpLast)]
        static void Finalizer(bool __state)
        {
            if (__state)
                Rand.PopState();
        }
    }
}
