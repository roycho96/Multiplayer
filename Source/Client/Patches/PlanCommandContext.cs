using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;

namespace Multiplayer.Client.Patches
{
    /// <summary>
    /// Fix for a desync in <c>Designator_Plan_Add.PlanCells</c> where the
    /// branch decision depends on <c>Find.Selector.SelectedPlan</c> — a
    /// per-client UI state that differs across multiplayer clients.
    ///
    /// Vanilla <c>PlanCells</c> reads <c>SelectedPlan</c> and, if it is
    /// non-null, extends the existing plan; otherwise it either adopts
    /// a matching existing plan from the selected cells or calls
    /// <c>CreateNewPlan</c> (which consumes a new Plan ID and advances
    /// the Rand state). Because <c>Find.Selector.SelectedPlan</c> reflects
    /// the local player's UI selection, three clients replaying the same
    /// command will branch differently and desync.
    ///
    /// Fix:
    ///   1. The command issuer serializes their <c>Find.Selector.SelectedPlan</c>
    ///      into the designator payload (see <c>DesignatorPatches.WriteData</c>).
    ///   2. On replay, <c>AsyncTimeComp.HandleDesignator.SetState</c> reads the
    ///      Plan reference and stores it in <see cref="overridePlan"/> while
    ///      setting <see cref="replaying"/> to true. <c>RestoreState</c>
    ///      clears both afterwards.
    ///   3. The <see cref="SelectedPlan_Getter_Override"/> and
    ///      <see cref="SelectedPlan_Setter_Override"/> Harmony patches
    ///      below intercept <c>Designator_Plan_Add.SelectedPlan</c> during
    ///      replay and route reads/writes to <see cref="overridePlan"/>
    ///      instead of <c>Find.Selector</c>, keeping UI state untouched
    ///      and the branch decision consistent across clients.
    ///
    /// <c>ThreadStatic</c> is used so the override can't leak across
    /// threads (command replay runs on the main thread, but safety).
    /// </summary>
    public static class PlanCommandContext
    {
        [System.ThreadStatic] public static bool replaying;
        [System.ThreadStatic] public static Plan overridePlan;
    }

    [HarmonyPatch(typeof(Designator_Plan_Add), "SelectedPlan", MethodType.Getter)]
    public static class SelectedPlan_Getter_Override
    {
        static bool Prefix(ref Plan __result)
        {
            if (!PlanCommandContext.replaying) return true;
            __result = PlanCommandContext.overridePlan;
            return false;
        }
    }

    [HarmonyPatch(typeof(Designator_Plan_Add), "SelectedPlan", MethodType.Setter)]
    public static class SelectedPlan_Setter_Override
    {
        // Mirrors the vanilla setter's colorDef side-effect without
        // touching Find.Selector during command replay.
        private static readonly FieldInfo colorDefField =
            AccessTools.Field(typeof(Designator_Plan_Add), "colorDef");

        static bool Prefix(Designator_Plan_Add __instance, Plan value)
        {
            if (!PlanCommandContext.replaying) return true;

            PlanCommandContext.overridePlan = value;
            // Vanilla setter also updates colorDef from the selected plan
            // (only when non-null). Replicate so PlanCells' subsequent
            // reads stay consistent.
            if (value != null && colorDefField != null)
                colorDefField.SetValue(__instance, value.Color);

            return false;
        }
    }
}
