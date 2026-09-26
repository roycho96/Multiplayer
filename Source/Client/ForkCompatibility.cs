using System;
using System.Reflection;
using HarmonyLib;
using Multiplayer.Common;
using RimWorld;
using Verse;

namespace Multiplayer.Client;

internal static class ForkCompatibility
{
    public static bool CheckMods()
    {
        var compatType = AccessTools.TypeByName("Multiplayer.Compat.MpCompat");
        if (compatType == null)
            return true;

        var marker = compatType.GetField(nameof(MpVersion.ForkCompatibilityRevision), BindingFlags.Public | BindingFlags.Static);
        if (marker is { IsLiteral: true, FieldType: var type } && type == typeof(int)
            && (int)marker.GetRawConstantValue() == MpVersion.ForkCompatibilityRevision
            && compatType.Assembly.GetType("Multiplayer.Compat.VanillaPatches") == null)
            return true;

        return Reject("This Multiplayer fork requires the matching roycho96 Multiplayer Compatibility revision 1. "
            + "Update both mods together before starting or joining a multiplayer game.");
    }

    public static bool CheckReplay(int protocol)
    {
        if (protocol == MpVersion.Protocol)
            return true;

        return Reject($"This replay uses Multiplayer protocol {protocol}; this fork requires {MpVersion.Protocol}. "
            + "Open it with the original mod versions, save as a single-player .rws file, then host that save with the updated mods. "
            + "Recorded commands cannot be migrated automatically.");
    }

    private static bool Reject(string reason)
    {
        Log.Error(reason);
        Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
        return false;
    }
}
