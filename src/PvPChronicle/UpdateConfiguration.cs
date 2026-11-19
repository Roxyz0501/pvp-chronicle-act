using System.Linq;

namespace PvPChronicle
{
    internal static class UpdateConfiguration
    {
        public const string RepositoryOwner = "Roxyz0501";
        public const string RepositoryName = "pvp-chronicle-act";
        public const string PluginAssemblyName = "PvPChronicle";
        public const string UpdaterAssemblyName = "PvPChronicle.Updater";
        public const string PluginFileName = "PvPChronicle.dll";
        public const string UpdaterFileName = "PvPChronicle.Updater.exe";
        public const string RepositoryUrl = "https://github.com/Roxyz0501/pvp-chronicle-act";
        public static string LatestReleaseUrl => RepositoryUrl + "/releases/latest";

        public static bool IsConfigured => IsSafeSegment(RepositoryOwner) && IsSafeSegment(RepositoryName);


        private static bool IsSafeSegment(string value) => !string.IsNullOrWhiteSpace(value) &&
            value.All(x => char.IsLetterOrDigit(x) || x == '-' || x == '_' || x == '.');
    }
}

