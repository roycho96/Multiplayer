using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace MultiplayerLoader
{
    // This class has to be named Multiplayer for backwards compatibility of the settings file location
    public class Multiplayer : Mod
    {
        public static Multiplayer instance;
        public static Action<Rect> settingsWindowDrawer;

        public Multiplayer(ModContentPack content) : base(content)
        {
            var harmony = typeof(Harmony).Assembly;
            var version = harmony.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            if (!Version.TryParse(version, out var parsed) || parsed.Major < 3)
                throw new InvalidOperationException($"Multiplayer for RimWorld 1.7 requires Harmony 3 (loaded {version ?? harmony.FullName}).");

            instance = this;
            var assembly = Content.assemblies.loadedAssemblies.FirstOrDefault(assembly => assembly.GetName().Name == "Multiplayer")
                ?? throw new InvalidOperationException("Missing Multiplayer.dll in the mod's 1.7 Assemblies folder.");
            assembly.GetType("Multiplayer.Client.Multiplayer", throwOnError: true)!.GetMethod("InitMultiplayer")!.Invoke(null, [content]);
        }

        public static Type? FindTypeInAppDomain(string typeFullName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeFullName, throwOnError: false, ignoreCase: false))
                .FirstOrDefault(type => type != null);

        public override string SettingsCategory() => "Multiplayer";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            settingsWindowDrawer?.Invoke(inRect);
        }
    }
}
