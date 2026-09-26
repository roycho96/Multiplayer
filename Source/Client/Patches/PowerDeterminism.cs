using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Multiplayer.Client.Patches;

internal static class StableComponentOrder
{
    internal static bool InSimulation => Multiplayer.Client != null
        && (Multiplayer.Ticking || Multiplayer.ExecutingCmds || Multiplayer.reloading);

    internal static void SortIfNeeded<T>(List<T> items, Comparison<T> comparison)
    {
        for (var i = 1; i < items.Count; i++)
        {
            if (comparison(items[i - 1], items[i]) <= 0)
                continue;

            items.Sort(comparison);
            return;
        }
    }

    internal static int Compare(ThingComp left, ThingComp right)
    {
        if (ReferenceEquals(left, right))
            return 0;

        var leftParent = left?.parent;
        var rightParent = right?.parent;
        var order = (leftParent?.thingIDNumber ?? int.MaxValue)
            .CompareTo(rightParent?.thingIDNumber ?? int.MaxValue);
        if (order != 0)
            return order;

        // Multiple components on one thing retain their definition order.
        return (leftParent?.AllComps?.IndexOf(left) ?? int.MaxValue)
            .CompareTo(rightParent?.AllComps?.IndexOf(right) ?? int.MaxValue);
    }

    internal static void Sort<T>(List<T> comps) where T : ThingComp
        => SortIfNeeded(comps, Compare);

    internal static int NetworkKey(PowerNet net)
    {
        var key = int.MaxValue;
        Include(net.transmitters);
        Include(net.powerComps);
        Include(net.batteryComps);
        return key;

        void Include<T>(List<T> comps) where T : ThingComp
        {
            foreach (var comp in comps)
                if (comp?.parent != null)
                    key = Math.Min(key, comp.parent.thingIDNumber);
        }
    }
}

[HarmonyPatch(typeof(BreakdownManager), nameof(BreakdownManager.MapComponentTick))]
internal static class BreakdownComponentOrder
{
    static void Prefix(BreakdownManager __instance)
    {
        if (StableComponentOrder.InSimulation && Find.TickManager.TicksGame % BreakdownManager.CheckIntervalTicks == 0)
            StableComponentOrder.Sort(__instance.comps);
    }
}

[HarmonyPatch(typeof(PowerNet), nameof(PowerNet.CurrentEnergyGainRate))]
internal static class PowerTraderOrder
{
    static void Prefix(PowerNet __instance)
    {
        if (StableComponentOrder.InSimulation)
            StableComponentOrder.Sort(__instance.powerComps);
    }
}

[HarmonyPatch(typeof(PowerNet), nameof(PowerNet.CurrentStoredEnergy))]
internal static class PowerBatteryOrder
{
    static void Prefix(PowerNet __instance)
    {
        if (StableComponentOrder.InSimulation)
            StableComponentOrder.Sort(__instance.batteryComps);
    }
}

[HarmonyPatch(typeof(PowerNetManager), nameof(PowerNetManager.PowerNetsTick))]
internal static class PowerNetworkOrder
{
    static void Prefix(PowerNetManager __instance)
    {
        if (StableComponentOrder.InSimulation)
            StableComponentOrder.SortIfNeeded(__instance.AllNetsListForReading,
                (left, right) => StableComponentOrder.NetworkKey(left).CompareTo(StableComponentOrder.NetworkKey(right)));
    }
}
