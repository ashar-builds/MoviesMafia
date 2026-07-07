using System.IO;

namespace AdBlockCore;

/// <summary>
/// Holds parsed <see cref="AbpRule"/>s and decides, per request, whether to block it —
/// the same block/exception model uBlock Origin uses on network rules.
///
/// Lookup is bucketed by keyword: each rule is filed under a token from its pattern, so a
/// request only tests rules whose keyword appears in the URL (plus a small "general" bucket
/// of rules with no usable keyword). This keeps matching fast even with 100k+ rules.
///
/// Decision order matches ABP semantics: an exception (@@) rule wins over a block rule.
/// </summary>
public sealed class AdBlockEngine
{
    private readonly Dictionary<string, List<AbpRule>> _blockByKeyword = new(StringComparer.Ordinal);
    private readonly List<AbpRule> _blockGeneral = new();
    private readonly Dictionary<string, List<AbpRule>> _allowByKeyword = new(StringComparer.Ordinal);
    private readonly List<AbpRule> _allowGeneral = new();

    // Optional: set once at startup once the PSL has loaded. Null until then, during which
    // ShouldBlock/Explain fall back to the naive last-two-labels comparison — filtering must
    // work immediately on first launch, before the PSL download completes.
    private PublicSuffixClassifier? _siteClassifier;

    public int RuleCount { get; private set; }
    public int SkippedLines { get; private set; }

    public void SetSiteClassifier(PublicSuffixClassifier classifier) => _siteClassifier = classifier;

    public void AddRule(AbpRule rule)
    {
        var (byKeyword, general) = rule.IsException
            ? (_allowByKeyword, _allowGeneral)
            : (_blockByKeyword, _blockGeneral);

        if (rule.Keyword is { } kw)
        {
            if (!byKeyword.TryGetValue(kw, out var list))
                byKeyword[kw] = list = new List<AbpRule>();
            list.Add(rule);
        }
        else
        {
            general.Add(rule);
        }
        RuleCount++;
    }

    /// <summary>Parse an entire filter-list file and add every supported network rule.
    /// Cosmetic lines (<c>##</c>, <c>#@#</c>, …) are NOT counted as skipped here — they're
    /// handled by <see cref="CosmeticEngine"/>, which the caller feeds the same text to; see
    /// <see cref="FilterListProvider.BuildEnginesAsync"/>.</summary>
    public void LoadFilterList(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] is '!' or '[' || CosmeticRule.IsCosmeticSyntax(trimmed)) continue;

            var rule = AbpRule.TryParse(trimmed);
            if (rule is not null) AddRule(rule);
            else SkippedLines++;
        }
    }

    public void LoadFilterFile(string path) => LoadFilterList(File.ReadAllText(path));

    /// <summary>
    /// True if the request to <paramref name="requestUrl"/> should be cancelled.
    /// <paramref name="documentUrl"/> is the page the request originates from (for third-party
    /// and $domain= evaluation). <paramref name="resourceType"/> narrows matching to
    /// type-scoped rules (<c>$script</c>, <c>$image</c>, …); pass <see cref="ResourceType.None"/>
    /// if the caller has no type signal.
    /// </summary>
    public bool ShouldBlock(string requestUrl, string? documentUrl, ResourceType resourceType = ResourceType.None)
    {
        var docDomain = DomainOf(documentUrl);
        var reqDomain = DomainOf(requestUrl);
        bool thirdParty = docDomain.Length > 0 && reqDomain.Length > 0 &&
                          !IsSameSite(reqDomain, docDomain);

        // A blocking rule must match first; only then do we pay for the exception check.
        if (!AnyMatch(_blockByKeyword, _blockGeneral, requestUrl, thirdParty, docDomain, resourceType))
            return false;

        if (AnyMatch(_allowByKeyword, _allowGeneral, requestUrl, thirdParty, docDomain, resourceType))
            return false;   // an @@ exception un-blocks it

        return true;
    }

    /// <summary>Diagnostic: return the block rule and any overriding exception rule for a request.</summary>
    public (AbpRule? block, AbpRule? allow) Explain(string requestUrl, string? documentUrl, ResourceType resourceType = ResourceType.None)
    {
        var docDomain = DomainOf(documentUrl);
        var reqDomain = DomainOf(requestUrl);
        bool thirdParty = docDomain.Length > 0 && reqDomain.Length > 0 &&
                          !IsSameSite(reqDomain, docDomain);
        var block = FirstMatch(_blockByKeyword, _blockGeneral, requestUrl, thirdParty, docDomain, resourceType);
        if (block is null) return (null, null);
        var allow = FirstMatch(_allowByKeyword, _allowGeneral, requestUrl, thirdParty, docDomain, resourceType);
        return (block, allow);
    }

    private static AbpRule? FirstMatch(Dictionary<string, List<AbpRule>> byKeyword, List<AbpRule> general,
                                       string url, bool thirdParty, string docDomain, ResourceType resourceType)
    {
        var lower = url.ToLowerInvariant();
        foreach (var (kw, list) in byKeyword)
        {
            if (!lower.Contains(kw, StringComparison.Ordinal)) continue;
            foreach (var rule in list)
                if (rule.Matches(url, thirdParty, docDomain, resourceType)) return rule;
        }
        foreach (var rule in general)
            if (rule.Matches(url, thirdParty, docDomain, resourceType)) return rule;
        return null;
    }

    private static bool AnyMatch(Dictionary<string, List<AbpRule>> byKeyword, List<AbpRule> general,
                                 string url, bool thirdParty, string docDomain, ResourceType resourceType)
    {
        var lower = url.ToLowerInvariant();

        // Only probe keyword buckets whose token actually occurs in the URL.
        foreach (var (kw, list) in byKeyword)
        {
            if (!lower.Contains(kw, StringComparison.Ordinal)) continue;
            foreach (var rule in list)
                if (rule.Matches(url, thirdParty, docDomain, resourceType)) return true;
        }

        foreach (var rule in general)
            if (rule.Matches(url, thirdParty, docDomain, resourceType)) return true;

        return false;
    }

    /// <summary>Same-site test for third-party classification: PSL-based (handles <c>.co.uk</c>-
    /// style multi-part TLDs correctly) once <see cref="SetSiteClassifier"/> has been called,
    /// naive last-two-labels before that (e.g. during the brief window before the PSL loads).</summary>
    private bool IsSameSite(string a, string b) =>
        _siteClassifier is not null ? _siteClassifier.IsSameSite(a, b) : NaiveIsSameSite(a, b);

    private static bool NaiveIsSameSite(string a, string b)
    {
        if (a == b) return true;
        return LastTwoLabels(a) == LastTwoLabels(b);
    }

    private static string LastTwoLabels(string host)
    {
        var parts = host.Split('.');
        return parts.Length <= 2 ? host : $"{parts[^2]}.{parts[^1]}";
    }

    private static string DomainOf(string? url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        return Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host.ToLowerInvariant() : "";
    }
}
