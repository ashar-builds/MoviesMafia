using System.Text.RegularExpressions;

namespace AdBlockCore;

/// <summary>
/// A single parsed Adblock Plus / uBlock Origin "cosmetic" rule — the kind that hides an
/// in-page element by CSS selector rather than blocking a network request. This is the
/// mechanism that removes ad *containers* whose markup is served inline (no separate request
/// for <see cref="AbpRule"/> to block).
///
/// Scope (see HANDOFF.md task 1): only plain CSS-selector hides (<c>##</c>) and their
/// exceptions (<c>#@#</c>) are implemented. uBO's extended/procedural syntax (<c>#?#</c>,
/// <c>:has-text()</c>, <c>:matches-css()</c>, …) and scriptlet injection (<c>##+js(...)</c>,
/// <c>#$#</c>) are NOT implemented — <see cref="TryParse"/> returns null for them so the
/// caller can count them as skipped rather than silently mis-applying a partial rule.
/// </summary>
public sealed class CosmeticRule
{
    public bool IsException { get; }
    public string Selector { get; }
    public string[]? IncludeDomains { get; }   // domain.com##.ad            — apply only here (+ subdomains)
    public string[]? ExcludeDomains { get; }   // ~domain.com##.ad           — apply everywhere EXCEPT here
    public string Source { get; }

    private CosmeticRule(bool isException, string selector, string[]? include, string[]? exclude, string source)
    {
        IsException = isException;
        Selector = selector;
        IncludeDomains = include;
        ExcludeDomains = exclude;
        Source = source;
    }

    // Separators checked most-specific-first so e.g. "#@?#" isn't mistaken for "#?#".
    // Only "##" and "#@#" are ones we act on; the rest are recognized so TryParse can
    // distinguish "not a cosmetic rule at all" from "cosmetic rule we deliberately skip".
    private static readonly string[] SeparatorsInPriorityOrder = { "#@?#", "#?#", "#@$#", "#$#", "#@#", "##" };

    /// <summary>True if the line uses cosmetic syntax at all (any known separator) — including
    /// the procedural/scriptlet forms this parser doesn't support. Lets callers tally "skipped
    /// because unsupported" separately from "not a cosmetic rule".</summary>
    public static bool IsCosmeticSyntax(string raw) =>
        FindSeparator(raw.Trim()) is not null;

    public static CosmeticRule? TryParse(string raw)
    {
        var line = raw.Trim();
        if (line.Length == 0 || line[0] is '!' or '[') return null;

        var found = FindSeparator(line);
        if (found is null) return null;
        var (sepIndex, sep) = found.Value;

        // Only plain hide ("##") and its exception ("#@#") are in scope — see class remarks.
        if (sep is not ("##" or "#@#")) return null;

        var domainsPart = line[..sepIndex];
        var selector = line[(sepIndex + sep.Length)..].Trim();
        if (selector.Length == 0) return null;

        // "##+js(...)" is a scriptlet injected through the plain-hide separator — still out of
        // scope even though the separator matched; a scriptlet selector is a directive, not CSS.
        if (selector.StartsWith("+js(", StringComparison.OrdinalIgnoreCase)) return null;

        if (!IsSafeSelector(selector)) return null;

        string[]? include = null, exclude = null;
        if (domainsPart.Length > 0)
        {
            var inc = new List<string>();
            var exc = new List<string>();
            foreach (var d in domainsPart.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var token = d.Trim();
                if (token.Length == 0) continue;
                if (token.StartsWith('~')) exc.Add(token[1..].ToLowerInvariant());
                else inc.Add(token.ToLowerInvariant());
            }
            if (inc.Count > 0) include = inc.ToArray();
            if (exc.Count > 0) exclude = exc.ToArray();
        }

        return new CosmeticRule(sep == "#@#", selector, include, exclude, raw.Trim());
    }

    private static (int index, string sep)? FindSeparator(string line)
    {
        (int index, string sep)? best = null;
        foreach (var sep in SeparatorsInPriorityOrder)
        {
            var idx = line.IndexOf(sep, StringComparison.Ordinal);
            if (idx < 0) continue;
            if (best is null || idx < best.Value.index) best = (idx, sep);
        }
        return best;
    }

    // Bare tag-name (or universal) selectors with no class/id/attribute/pseudo qualifier are
    // rejected outright: a well-formed ad-list entry always narrows with a class/id/attribute,
    // and a rule that resolves to exactly one of these WOULD strip real page content — most
    // dangerously the provider's own <video>/<iframe> embed. See HANDOFF's "Correctness
    // guardrails" — this is the guard against a bad generic rule breaking the video.
    private static readonly HashSet<string> DangerousBareSelectors = new(StringComparer.OrdinalIgnoreCase)
    {
        "*", "html", "body", "video", "audio", "iframe", "source", "embed", "object", "canvas",
    };

    private static readonly Regex BareTagName = new(@"^[a-zA-Z][a-zA-Z0-9]*$", RegexOptions.Compiled);

    /// <summary>Rejects a selector if ANY top-level comma branch is a bare, unqualified tag name
    /// (or <c>*</c>) that's dangerous to hide site-wide. Compound/qualified selectors — even ones
    /// that mention a risky tag, like <c>iframe.ad-slot</c> — are unaffected.</summary>
    private static bool IsSafeSelector(string selector)
    {
        foreach (var branch in selector.Split(','))
        {
            var t = branch.Trim();
            if (t.Length == 0) return false;
            if (t == "*") return false;
            if (BareTagName.IsMatch(t) && DangerousBareSelectors.Contains(t)) return false;
        }
        return true;
    }
}
