using System.Linq;
using System.Reflection;

namespace Multiplayer.Common
{
    public static class MpVersion
    {
        public const string SimpleVersion = "0.11.5";
        public const string ForkVersion = SimpleVersion + "-roycho96.2";
        // The fork adds SelectedPlan command data and uses upstream protocol 57's join metadata.
        public const int Protocol = 57009602;
        public const int ForkCompatibilityRevision = 1;

        public static readonly string? GitHash = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == "GitHash")?.Value;

        public static readonly string Version = ForkVersion + (GitHash != null ? $"+{GitHash}" : "");

        public static readonly string? GitDescription = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == "GitDescription")?.Value;

        public const string ApiAssemblyName = "0MultiplayerAPI";

#if DEBUG
        public const bool IsDebug = true;
#else
        public const bool IsDebug = false;
#endif
    }
}
