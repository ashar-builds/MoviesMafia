using System.IO;
using System.Net.Http;
using Nager.PublicSuffix;
using Nager.PublicSuffix.RuleProviders;

namespace AdBlockCore;

/// <summary>
/// Registrable-domain ("same site") comparison backed by the real Mozilla Public Suffix List,
/// replacing <see cref="AdBlockEngine"/>'s old last-two-labels heuristic — which misclassifies
/// multi-part TLDs (<c>foo.co.uk</c> vs <c>bar.co.uk</c> are NOT the same site, but the naive
/// heuristic said they were because it only ever compares the last two labels).
///
/// Nager.PublicSuffix is a plain portable .NET library (no WebView2/WPF dependency), so pulling
/// it into <c>AdBlock/</c> doesn't violate the "engine stays host-agnostic" rule.
///
/// Caching is done by hand (download once, reuse the file, `LocalFileRuleProvider` reads it) —
/// matching <see cref="FilterListProvider"/>'s existing cache-file pattern, and sidestepping
/// <c>CachedHttpRuleProvider</c>'s <c>ICacheProvider</c> plumbing, whose
/// <c>LocalFileSystemCacheProvider</c> proved unreliable in testing (BuildAsync silently
/// returned false / left DomainDataStructure unset even on a successful download).
/// </summary>
public sealed class PublicSuffixClassifier
{
    private const string DataUrl = "https://publicsuffix.org/list/public_suffix_list.dat";

    private readonly DomainParser? _parser;

    private PublicSuffixClassifier(DomainParser? parser) => _parser = parser;

    /// <summary>
    /// Ensures the public suffix list is cached locally (downloading if stale/missing) and
    /// builds a classifier from it. Never throws: if the download fails and no cache exists,
    /// returns a classifier that falls back to the naive heuristic rather than leaving the
    /// engine without any third-party signal at all.
    /// </summary>
    public static async Task<PublicSuffixClassifier> CreateAsync(string cacheDir, TimeSpan? maxAge = null,
                                                                   Action<string>? log = null)
    {
        Directory.CreateDirectory(cacheDir);
        var path = Path.Combine(cacheDir, "public_suffix_list.dat");
        var age = maxAge ?? TimeSpan.FromDays(4);
        var fresh = File.Exists(path) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)) < age;

        if (!fresh)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var text = await http.GetStringAsync(DataUrl);
                await File.WriteAllTextAsync(path, text);
            }
            catch (Exception ex)
            {
                log?.Invoke($"  Public Suffix List download failed ({ex.Message}); using cache if present.");
            }
        }

        if (!File.Exists(path))
        {
            log?.Invoke("  Public Suffix List unavailable — using last-two-labels fallback.");
            return new PublicSuffixClassifier(null);
        }

        try
        {
            var ruleProvider = new LocalFileRuleProvider(path);
            await ruleProvider.BuildAsync();
            return new PublicSuffixClassifier(new DomainParser(ruleProvider));
        }
        catch (Exception ex)
        {
            log?.Invoke($"  Public Suffix List failed to parse ({ex.Message}); using last-two-labels fallback.");
            return new PublicSuffixClassifier(null);
        }
    }

    /// <summary>True if both hosts share the same registrable domain (PSL-correct — handles
    /// <c>.co.uk</c>-style multi-part TLDs). Falls back to the naive last-two-labels comparison
    /// for hosts the PSL can't parse (bare hostnames like <c>localhost</c>, raw IPs) or if the
    /// list failed to load.</summary>
    public bool IsSameSite(string hostA, string hostB)
    {
        if (hostA == hostB) return true;

        if (_parser is not null)
        {
            var a = TryGetRegistrableDomain(hostA);
            var b = TryGetRegistrableDomain(hostB);
            if (a is not null && b is not null) return a == b;
        }

        return NaiveLastTwoLabels(hostA) == NaiveLastTwoLabels(hostB);
    }

    private string? TryGetRegistrableDomain(string host)
    {
        try
        {
            return _parser!.Parse(host)?.RegistrableDomain;
        }
        catch
        {
            // Not a PSL-parseable name (localhost, bare IP, single-label host, etc.) —
            // let the caller fall back to the naive comparison.
            return null;
        }
    }

    private static string NaiveLastTwoLabels(string host)
    {
        var parts = host.Split('.');
        return parts.Length <= 2 ? host : $"{parts[^2]}.{parts[^1]}";
    }
}
