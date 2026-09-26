using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace Multiplayer.Client.Patches
{
    [HarmonyPatch(typeof(FishShadowComponent), nameof(FishShadowComponent.MapComponentTick))]
    public static class FishShadowComponent_MapComponentTick_Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo objectHash = AccessTools.Method(typeof(object), nameof(object.GetHashCode));
            MethodInfo waterBodyHash = AccessTools.Method(typeof(FishShadowComponent_MapComponentTick_Patch), nameof(FishShadowComponent_MapComponentTick_Patch.WaterBodyHash));

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(objectHash))
                    yield return new CodeInstruction(OpCodes.Call, waterBodyHash);
                else
                    yield return instruction;
            }
        }

        public static int WaterBodyHash(WaterBody body)
        {
            if (Multiplayer.Client == null)
                return body.GetHashCode();

            return Gen.HashCombineInt(body.map?.uniqueID ?? 0, body.rootCell.x, body.rootCell.z, (int)body.waterBodyType);
        }
    }

    [HarmonyPatch(typeof(FishShadowComponent), "FindFishFleckLocation")]
    static class FishShadowComponent_FindFishFleckLocation_Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var replacement = AccessTools.Method(
                typeof(FishShadowComponent_FindFishFleckLocation_Patch),
                nameof(StableRandomElement));
            var patched = 0;

            foreach (var instruction in instructions)
            {
                if (instruction.operand is MethodInfo method &&
                    method.Name == nameof(GenCollection.RandomElement) &&
                    method.ReturnType == typeof(IntVec3))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    patched++;
                }

                yield return instruction;
            }

            if (patched != 1)
                Log.Error($"Fish shadow cell ordering patch expected 1 call, found {patched}");
        }

        public static IntVec3 StableRandomElement(HashSet<IntVec3> cells)
        {
            if (Multiplayer.Client == null || cells == null || cells.Count == 0)
                return cells.RandomElement();

            var ordered = new List<IntVec3>(cells);
            ordered.Sort((a, b) =>
            {
                var x = a.x.CompareTo(b.x);
                if (x != 0) return x;

                var z = a.z.CompareTo(b.z);
                return z != 0 ? z : a.y.CompareTo(b.y);
            });

            return ordered.RandomElement();
        }
    }


    // Suppress auto-popup of Dialog_NamePlayerGravship in MP.
    // Manual rename via inspect tab (IRenameable) is already synced by SyncMethods.
    [HarmonyPatch(typeof(Building_GravEngine), nameof(Building_GravEngine.UpdateSubstructureIfNeeded))]
    public static class SuppressGravshipNamingDialog
    {
        static void Prefix(ref bool ___haveShownNameDialog)
        {
            if (Multiplayer.Client == null) return;

            ___haveShownNameDialog = true;
        }
    }

    [HarmonyPatch(typeof(CompStatue), nameof(CompStatue.InitFakePawn))]
    public static class PatchInitFakePawnToNotSyncPawnName
    {
        static void Prefix(ref bool __state)
        {
            if (Multiplayer.Client == null) return;

            if(!Multiplayer.dontSync)
            {
                Multiplayer.dontSync = true;
                __state = true;
            }
        }

        static void Finalizer(bool __state)
        {
            if (__state)
                Multiplayer.dontSync = false;
        }
    }
}
