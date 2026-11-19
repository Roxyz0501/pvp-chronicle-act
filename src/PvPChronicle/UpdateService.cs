using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PvPChronicle
{
    internal sealed class UpdateService : IDisposable
    {
        private const long MaximumDownloadBytes = 100L * 1024 * 1024;
        private readonly HttpClient client;

        public UpdateService()
        {
            client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PvPChronicle/" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken)
        {
            if (!UpdateConfiguration.IsConfigured)
                return new UpdateCheckResult { Status = UpdateCheckStatus.RepositoryNotConfigured };
            try
            {
                var tag = await ResolveLatestStableTagAsync(cancellationToken).ConfigureAwait(false);
                return await CheckForTagAsync(() => Task.FromResult(tag),
                    value => FetchReleaseApiAsync(value, cancellationToken), currentVersion).ConfigureAwait(false);
            }
            catch (UpdateRateLimitException ex) { return new UpdateCheckResult { Status = UpdateCheckStatus.RateLimited, Error = ex.Message }; }
            catch (Exception ex)
            {
                return new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Error = ex.GetBaseException().Message };
            }
        }

        internal async Task<string> ResolveLatestStableTagAsync(CancellationToken cancellationToken)
        {
            var uri = new Uri(UpdateConfiguration.LatestReleaseUrl);
            EnsureAllowedUri(uri, false);
            for (var redirects = 0; redirects <= 5; redirects++)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, uri))
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed || response.StatusCode == System.Net.HttpStatusCode.NotImplemented)
                        return await ResolveLatestStableTagWithGetAsync(uri, cancellationToken).ConfigureAwait(false);
                    var code = (int)response.StatusCode;
                    if (code == 403 || code == 429) throw new UpdateRateLimitException("RateLimited");
                    if (code >= 300 && code < 400)
                    {
                        var location = response.Headers.Location;
                        if (location == null || redirects == 5) throw new InvalidDataException("InvalidLatestReleaseRedirect");
                        uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                        ValidateLatestRedirectHost(uri);
                        continue;
                    }
                    response.EnsureSuccessStatusCode();
                    return ParseLatestStableTagUri(uri);
                }
            }
            throw new InvalidDataException("InvalidLatestReleaseRedirect");
        }

        private async Task<string> ResolveLatestStableTagWithGetAsync(Uri uri, CancellationToken cancellationToken)
        {
            for (var redirects = 0; redirects <= 5; redirects++)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    var code = (int)response.StatusCode;
                    if (code == 403 || code == 429) throw new UpdateRateLimitException("RateLimited");
                    if (code >= 300 && code < 400)
                    {
                        var location = response.Headers.Location;
                        if (location == null || redirects == 5) throw new InvalidDataException("InvalidLatestReleaseRedirect");
                        uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                        ValidateLatestRedirectHost(uri);
                        continue;
                    }
                    response.EnsureSuccessStatusCode();
                    return ParseLatestStableTagUri(uri);
                }
            }
            throw new InvalidDataException("InvalidLatestReleaseRedirect");
        }

        internal static string ParseLatestStableTagUri(Uri uri)
        {
            if (uri == null || uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidDataException("InvalidLatestReleaseRedirect");
            var segments = uri.AbsolutePath.Split('/');
            if (segments.Length != 6 || !string.Equals(Uri.UnescapeDataString(segments[1]), UpdateConfiguration.RepositoryOwner, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Uri.UnescapeDataString(segments[2]), UpdateConfiguration.RepositoryName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(segments[3], "releases", StringComparison.OrdinalIgnoreCase) || !string.Equals(segments[4], "tag", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("InvalidLatestReleaseRedirect");
            var tag = Uri.UnescapeDataString(segments[5]); SemVersion version;
            if (!SemVersion.TryParse(tag, out version) || !version.IsStable) throw new InvalidDataException("InvalidLatestReleaseTag");
            return tag;
        }

        internal static void ValidateLatestRedirectHost(Uri uri)
        {
            if (uri == null || uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidDataException("InvalidLatestReleaseRedirect");
            var segments = uri.AbsolutePath.Split('/');
            if (segments.Length < 4 || !string.Equals(Uri.UnescapeDataString(segments[1]), UpdateConfiguration.RepositoryOwner, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Uri.UnescapeDataString(segments[2]), UpdateConfiguration.RepositoryName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("InvalidLatestReleaseRedirect");
        }

        internal static async Task<UpdateCheckResult> CheckForTagAsync(Func<Task<string>> latestTagFetcher,
            Func<string, Task<ReleaseApiResponse>> apiFetcher, Version currentVersion)
        {
            try
            {
                if (latestTagFetcher == null || apiFetcher == null) throw new ArgumentNullException();
                var tag = await latestTagFetcher().ConfigureAwait(false); SemVersion latest, current;
                if (!SemVersion.TryParse(tag, out latest) || !latest.IsStable)
                    return new UpdateCheckResult { Status = UpdateCheckStatus.NoStableRelease };
                if (!TryVersion(currentVersion, out current)) throw new FormatException("Current version is not valid SemVer.");
                if (latest.CompareTo(current) <= 0) return new UpdateCheckResult { Status = UpdateCheckStatus.UpToDate };

                var response = await apiFetcher(tag).ConfigureAwait(false);
                if (response == null) throw new InvalidDataException("EmptyReleaseResponse");
                if (response.StatusCode == 403 || response.StatusCode == 429)
                    return new UpdateCheckResult { Status = UpdateCheckStatus.UpdateAvailable, Release = CreateTagPinnedRelease(tag, latest), ApiRateLimited = true };
                if (response.StatusCode != 200)
                    return new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Error = "ReleaseApiHttp" + response.StatusCode };

                var release = ReleaseParser.ParseStableReleases("[" + response.Body + "]").SingleOrDefault();
                if (release == null || !string.Equals(release.Tag, tag, StringComparison.Ordinal) || release.Version.CompareTo(latest) != 0)
                    throw new InvalidDataException("ReleaseTagMismatch");
                return new UpdateCheckResult { Status = UpdateCheckStatus.UpdateAvailable, Release = release };
            }
            catch (UpdateRateLimitException ex) { return new UpdateCheckResult { Status = UpdateCheckStatus.RateLimited, Error = ex.Message }; }
            catch (Exception ex) { return new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Error = ex.GetBaseException().Message }; }
        }

        private async Task<ReleaseApiResponse> FetchReleaseApiAsync(string tag, CancellationToken cancellationToken)
        {
            var url = "https://api.github.com/repos/" + UpdateConfiguration.RepositoryOwner + "/" + UpdateConfiguration.RepositoryName + "/releases/tags/" + Uri.EscapeDataString(tag);
            EnsureAllowedUri(new Uri(url), true);
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                var code = (int)response.StatusCode;
                if (code == 403 || code == 429) return new ReleaseApiResponse { StatusCode = code };
                if (code != 200) return new ReleaseApiResponse { StatusCode = code };
                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > 4L * 1024 * 1024)
                    throw new InvalidDataException("ReleaseApiResponseTooLarge");
                return new ReleaseApiResponse { StatusCode = code, Body = await ReadLimitedAsync(response.Content, 4 * 1024 * 1024, cancellationToken).ConfigureAwait(false) };
            }
        }

        private static ReleaseInfo CreateTagPinnedRelease(string tag, SemVersion version)
        {
            var zipName = "PvPChronicle-v" + version + ".zip";
            var manifestName = "PvPChronicle-v" + version + ".sha256";
            var prefix = UpdateConfiguration.RepositoryUrl + "/releases/download/" + Uri.EscapeDataString(tag) + "/";
            return new ReleaseInfo
            {
                Tag = tag, Version = version, Name = tag,
                Assets = new System.Collections.Generic.List<ReleaseAsset>
                {
                    new ReleaseAsset { Name = zipName, DownloadUrl = prefix + zipName },
                    new ReleaseAsset { Name = manifestName, DownloadUrl = prefix + manifestName }
                }
            };
        }

        private static bool TryVersion(Version version, out SemVersion parsed)
        {
            parsed = null;
            return version != null && SemVersion.TryParse(version.Major + "." + version.Minor + "." + Math.Max(0, version.Build), out parsed);
        }

        internal static async Task<UpdateCheckResult> CheckFromFetcherAsync(Func<Task<string>> fetcher, Version currentVersion)
        {
            try
            {
                if (fetcher == null) throw new ArgumentNullException("fetcher");
                return EvaluateResponse(await fetcher().ConfigureAwait(false), currentVersion);
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Error = ex.GetBaseException().Message };
            }
        }

        internal static UpdateCheckResult EvaluateResponse(string json, Version currentVersion)
        {
            try
            {
                var release = ReleaseParser.ParseStableReleases(json).FirstOrDefault();
                if (release == null) return new UpdateCheckResult { Status = UpdateCheckStatus.NoStableRelease };
                SemVersion parsedCurrent;
                if (currentVersion == null || !SemVersion.TryParse(currentVersion.Major + "." + currentVersion.Minor + "." + Math.Max(0, currentVersion.Build), out parsedCurrent))
                    throw new FormatException("Current version is not valid SemVer.");
                return new UpdateCheckResult
                {
                    Status = release.Version.CompareTo(parsedCurrent) > 0 ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate,
                    Release = release
                };
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Error = ex.GetBaseException().Message };
            }
        }

        public async Task<PreparedUpdate> DownloadAndVerifyAsync(ReleaseInfo release, CancellationToken cancellationToken)
        {
            if (release == null) throw new ArgumentNullException("release");
            var expectedZip = "PvPChronicle-v" + release.Version + ".zip";
            var alternateZip = "PvPChronicle-" + release.Version + ".zip";
            var zip = release.Assets.FirstOrDefault(x => string.Equals(x.Name, expectedZip, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Name, alternateZip, StringComparison.OrdinalIgnoreCase));
            var expectedManifest = "PvPChronicle-v" + release.Version + ".sha256";
            var manifest = release.Assets.FirstOrDefault(x => string.Equals(x.Name, expectedManifest, StringComparison.OrdinalIgnoreCase)) ??
                release.Assets.FirstOrDefault(x => string.Equals(x.Name, zip == null ? string.Empty : zip.Name + ".sha256", StringComparison.OrdinalIgnoreCase)) ??
                release.Assets.FirstOrDefault(x => string.Equals(x.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
            if (zip == null || manifest == null) throw new InvalidDataException("UpdateAssetMissing");
            var assetPrefix = "https://github.com/" + UpdateConfiguration.RepositoryOwner + "/" + UpdateConfiguration.RepositoryName + "/releases/download/" + release.Tag + "/";
            if (!string.Equals(zip.DownloadUrl, assetPrefix + zip.Name, StringComparison.Ordinal) || !string.Equals(manifest.DownloadUrl, assetPrefix + manifest.Name, StringComparison.Ordinal)) throw new InvalidDataException("InvalidPackage");
            if (zip.Size < 0 || zip.Size > MaximumDownloadBytes || manifest.Size < 0 || manifest.Size > 1024 * 1024) throw new InvalidDataException("InvalidPackage");

            var root = Path.Combine(Path.GetTempPath(), "PvPChronicleUpdater", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var zipPath = Path.Combine(root, "package.zip");
            var manifestPath = Path.Combine(root, "package.sha256");
            await DownloadAsync(zip.DownloadUrl, zipPath, MaximumDownloadBytes, cancellationToken).ConfigureAwait(false);
            await DownloadAsync(manifest.DownloadUrl, manifestPath, 1024 * 1024, cancellationToken).ConfigureAwait(false);
            var expected = UpdatePackageVerifier.FindManifestHash(File.ReadAllText(manifestPath, Encoding.UTF8), zip.Name);
            if (!UpdatePackageVerifier.VerifySha256(zipPath, expected)) throw new InvalidDataException("HashFailed");

            var extracted = Path.Combine(root, "extracted");
            UpdatePackageVerifier.ExtractValidated(zipPath, extracted);
            var dll = Directory.GetFiles(extracted, UpdateConfiguration.PluginFileName, SearchOption.AllDirectories).SingleOrDefault();
            var updater = Directory.GetFiles(extracted, UpdateConfiguration.UpdaterFileName, SearchOption.AllDirectories).SingleOrDefault();
            if (dll == null || updater == null) throw new InvalidDataException("InvalidPackage");
            UpdatePackageVerifier.ValidatePluginAssembly(dll, release.Version);
            UpdatePackageVerifier.ValidateUpdaterAssembly(updater, release.Version);
            return new PreparedUpdate { StagedDllPath = dll, StagedUpdaterPath = updater, DllSha256 = UpdatePackageVerifier.ComputeSha256(dll), UpdaterSha256 = UpdatePackageVerifier.ComputeSha256(updater), Release = release };
        }

        public void LaunchUpdater(PreparedUpdate update, string targetDllPath)
        {
            if (update == null) throw new ArgumentNullException("update");
            var stagedUpdater = Path.GetFullPath(update.StagedUpdaterPath);
            if (!File.Exists(stagedUpdater)) throw new InvalidOperationException("UpdaterMissing");
            UpdatePackageVerifier.ValidatePluginAssembly(update.StagedDllPath, update.Release == null ? null : update.Release.Version);
            UpdatePackageVerifier.ValidateUpdaterAssembly(stagedUpdater, update.Release == null ? null : update.Release.Version);
            if (!UpdatePackageVerifier.VerifySha256(update.StagedDllPath, update.DllSha256)) throw new InvalidDataException("HashFailed");
            if (!UpdatePackageVerifier.VerifySha256(stagedUpdater, update.UpdaterSha256)) throw new InvalidDataException("HashFailed");
            targetDllPath = Path.GetFullPath(targetDllPath ?? string.Empty);
            if (!File.Exists(targetDllPath) || !string.Equals(Path.GetFileName(targetDllPath), UpdateConfiguration.PluginFileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("PluginPathUnavailable");
            var originalHash = UpdatePackageVerifier.ComputeSha256(targetDllPath);
            var info = new ProcessStartInfo
            {
                FileName = stagedUpdater,
                WorkingDirectory = Path.GetDirectoryName(stagedUpdater),
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = BuildUpdaterArguments(update, targetDllPath, Process.GetCurrentProcess().Id, originalHash)
            };
            var updater = Process.Start(info);
            if (updater == null) throw new InvalidOperationException("UpdaterStartFailed");
            updater.Dispose();
        }

        internal static string BuildUpdaterArguments(PreparedUpdate update, string targetDllPath, int processId, string originalHash)
        {
            if (update == null || update.Release == null || update.Release.Version == null) throw new InvalidDataException("InvalidPackage");
            return "--wait-pid " + processId +
                " --source " + Quote(Path.GetFullPath(update.StagedDllPath)) + " --target " + Quote(Path.GetFullPath(targetDllPath)) +
                " --sha256 " + update.DllSha256 + " --original " + originalHash + " --version " + update.Release.Version;
        }

        private async Task DownloadAsync(string url, string destination, long maximumBytes, CancellationToken cancellationToken)
        {
            var uri = new Uri(url);
            EnsureAllowedUri(uri, false);
            HttpResponseMessage response = null;
            for (var redirect = 0; redirect <= 5; redirect++)
            {
                response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                var status = (int)response.StatusCode;
                if (status < 300 || status >= 400) break;
                var location = response.Headers.Location;
                response.Dispose(); response = null;
                if (location == null) throw new InvalidDataException("InvalidPackage");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                EnsureAllowedUri(uri, false);
            }
            if (response == null) throw new InvalidDataException("InvalidPackage");
            using (response)
            {
                if ((int)response.StatusCode == 403 || (int)response.StatusCode == 429) throw new UpdateRateLimitException("RateLimited");
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > maximumBytes)
                    throw new InvalidDataException("InvalidPackage");
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = File.Create(destination))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        total += read;
                        if (total > maximumBytes) throw new InvalidDataException("InvalidPackage");
                        await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        internal static void EnsureAllowedUri(Uri uri, bool api)
        {
            if (uri == null || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidDataException("Only HTTPS update URLs are allowed.");
            var host = uri.Host.ToLowerInvariant();
            var allowed = api ? host == "api.github.com" :
                host == "github.com" || host == "objects.githubusercontent.com" || host == "release-assets.githubusercontent.com";
            if (!allowed) throw new InvalidDataException("Unexpected update host.");
        }

        private static async Task<string> ReadLimitedAsync(HttpContent content, int maximumBytes, CancellationToken cancellationToken)
        {
            using (var input = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[81920]; int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    if (output.Length + read > maximumBytes) throw new InvalidDataException("ReleaseApiResponseTooLarge");
                    output.Write(buffer, 0, read);
                }
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains("\"")) throw new InvalidDataException("InvalidPackage");
            return "\"" + value + "\"";
        }
        public void Dispose() { client.Dispose(); }
    }

    internal sealed class UpdateRateLimitException : Exception
    {
        public UpdateRateLimitException(string message) : base(message) { }
    }
}
