using System.IO;
using System.Linq;
using System.Text;

namespace AdBlockCore;

/// <summary>
/// Holds parsed <see cref="CosmeticRule"/>s and builds, per hostname, the combined CSS
/// stylesheet of selectors that should be hidden on that host — the cosmetic-filtering
/// counterpart to <see cref="AdBlockEngine"/>'s network blocking.
///
/// Three buckets, mirroring uBO's own model:
///   generic hides    — apply on every site (<c>##.selector</c>), unless that site is in the
///                       rule's own exclude list (<c>~example.com##.selector</c> is really a
///                       generic hide that EXCLUDES example.com, not a domain-scoped hide).
///   domain hides     — apply only on the listed domain(s) + their subdomains
///                       (<c>example.com##.selector</c>).
///   domain exceptions — cancel a matching selector on the listed domain(s) (<c>#@#</c>); with
///                       no domain part, an exception cancels that selector everywhere.
/// </summary>
public sealed class CosmeticEngine
{
    private readonly HashSet<string> _genericHide = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _genericHideExcludedOn = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _domainHide = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _domainException = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _globalException = new(StringComparer.Ordinal);

    public int RuleCount { get; private set; }

    /// <summary>Lines using a cosmetic separator that weren't applied — procedural (<c>#?#</c>),
    /// scriptlet (<c>##+js</c>, <c>#$#</c>), or a plain-hide rule rejected by the bare-tag safety
    /// guard in <see cref="CosmeticRule.TryParse"/>.</summary>
    public int SkippedUnsupported { get; private set; }

    // Read-only views of the raw rule tables, for CosmeticInjector to serialize into the
    // per-frame JS payload (see its class remarks for why hostname matching happens in the
    // browser rather than being resolved host-side via BuildStylesheetFor).
    public IReadOnlyCollection<string> GenericHideSelectors => _genericHide;
    public IReadOnlyDictionary<string, IReadOnlyList<string>> GenericHideExclusions =>
        _genericHideExcludedOn.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DomainHideSelectors =>
        _domainHide.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DomainExceptionSelectors =>
        _domainException.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
    public IReadOnlyCollection<string> GlobalExceptionSelectors => _globalException;

    public void AddRule(CosmeticRule rule)
    {
        RuleCount++;

        if (rule.IsException)
        {
            if (rule.IncludeDomains is { } domains)
            {
                foreach (var d in domains)
                {
                    if (!_domainException.TryGetValue(d, out var list))
                        _domainException[d] = list = new List<string>();
                    list.Add(rule.Selector);
                }
            }
            else
            {
                _globalException.Add(rule.Selector);
            }
            return;
        }

        if (rule.IncludeDomains is { } include)
        {
            foreach (var d in include)
            {
                if (!_domainHide.TryGetValue(d, out var list))
                    _domainHide[d] = list = new List<string>();
                list.Add(rule.Selector);
            }
            return;
        }

        // No IncludeDomains: a generic hide, optionally excluding specific sites.
        _genericHide.Add(rule.Selector);
        if (rule.ExcludeDomains is { } exclude)
        {
            foreach (var d in exclude)
            {
                if (!_genericHideExcludedOn.TryGetValue(d, out var list))
                    _genericHideExcludedOn[d] = list = new List<string>();
                list.Add(rule.Selector);
            }
        }
    }

    /// <summary>Parse an entire filter-list file and add every supported cosmetic rule,
    /// tallying anything recognizably cosmetic-but-unsupported (procedural/scriptlet) rather
    /// than silently dropping it — see HANDOFF.md's "Do NOT silently pretend they're handled".</summary>
    public void LoadFilterList(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] is '!' or '[') continue;

            var rule = CosmeticRule.TryParse(trimmed);
            if (rule is not null) { AddRule(rule); continue; }

            if (CosmeticRule.IsCosmeticSyntax(trimmed)) SkippedUnsupported++;
        }
    }

    public void LoadFilterFile(string path) => LoadFilterList(File.ReadAllText(path));

    /// <summary>
    /// Builds the CSS for every selector that should be hidden on <paramref name="host"/>:
    /// generic hides not excluded on this host, plus domain hides matching this host (or an
    /// ancestor domain of it), minus anything cancelled by a matching <c>#@#</c> exception.
    /// Returns "" if there's nothing to hide (caller should skip injecting an empty <c>&lt;style&gt;</c>).
    /// </summary>
    public string BuildStylesheetFor(string host)
    {
        host = host.ToLowerInvariant();
        var selectors = new List<string>();

        foreach (var selector in _genericHide)
        {
            if (IsExcludedGenericOn(selector, host)) continue;
            selectors.Add(selector);
        }

        foreach (var (domain, list) in _domainHide)
        {
            if (!DomainMatches(host, domain)) continue;
            selectors.AddRange(list);
        }

        if (selectors.Count == 0) return "";

        var excepted = new HashSet<string>(_globalException, StringComparer.Ordinal);
        foreach (var (domain, list) in _domainException)
        {
            if (!DomainMatches(host, domain)) continue;
            foreach (var s in list) excepted.Add(s);
        }

        var kept = selectors.Where(s => !excepted.Contains(s)).Distinct().ToList();
        if (kept.Count == 0) return "";

        var sb = new StringBuilder();
        sb.Append(string.Join(", ", kept));
        sb.Append(" { display: none !important; }");
        return sb.ToString();
    }

    private bool IsExcludedGenericOn(string selector, string host)
    {
        foreach (var (domain, list) in _genericHideExcludedOn)
        {
            if (DomainMatches(host, domain) && list.Contains(selector, StringComparer.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>True if <paramref name="host"/> IS <paramref name="domain"/>, or a subdomain of it.</summary>
    private static bool DomainMatches(string host, string domain) =>
        host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);
}
