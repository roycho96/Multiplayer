using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Patches
{
    public static class PlanCommandContext
    {
        public readonly struct State
        {
            internal readonly bool active;
            internal readonly Plan plan;

            internal State(bool active, Plan plan)
            {
                this.active = active;
                this.plan = plan;
            }
        }

        [System.ThreadStatic] private static bool active;
        [System.ThreadStatic] private static Plan plan;

        public static State Enter(Plan selectedPlan)
        {
            var previous = new State(active, plan);
            active = true;
            plan = selectedPlan;
            return previous;
        }

        public static void Restore(State state)
        {
            active = state.active;
            plan = state.plan;
        }

        internal static bool TryGet(out Plan selectedPlan)
        {
            selectedPlan = plan;
            return active;
        }

        internal static bool TrySet(Designator_Plan_Add designator, Plan selectedPlan)
        {
            if (!active)
                return false;

            plan = selectedPlan;
            if (selectedPlan != null)
                designator.colorDef = selectedPlan.Color;

            return true;
        }
    }

    [HarmonyPatch(typeof(Designator_Plan_Add), "SelectedPlan", MethodType.Getter)]
    static class DesignatorPlanSelectedPlanGetterPatch
    {
        static bool Prefix(ref Plan __result) => !PlanCommandContext.TryGet(out __result);
    }

    [HarmonyPatch(typeof(Designator_Plan_Add), "SelectedPlan", MethodType.Setter)]
    static class DesignatorPlanSelectedPlanSetterPatch
    {
        static bool Prefix(Designator_Plan_Add __instance, Plan value) =>
            !PlanCommandContext.TrySet(__instance, value);
    }
}
