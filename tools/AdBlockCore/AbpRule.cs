using System.Text;
using System.Text.RegularExpressions;

namespace AdBlockCore;

/// <summary>
/// A single parsed Adblock Plus / EasyList "network" rule — the kind that blocks (or
/// un-blocks, when it's an <c>@@</c> exception) an HTTP request by URL pattern.
///
/// This is the same filter-list syntax uBlock Origin consumes. We implement the network
/// subset that maps cleanly onto WebView2's request interception:
///   ||domain^      anchor to a domain (+ subdomains)
///   |...|          start/end anchors
///   ^              separator placeholder
///   *              wildcard
///   /regex/        literal regex
///   $third-party, $~third-party, $domain=a.com|~b.com   options we honour
///
/// Cosmetic rules (##, #?#, #@#) and pure request *modifiers* ($csp, $removeparam,
/// $redirect, …) can't be expressed as a plain block/allow decision on WebResourceRequested,
/// so <see cref="TryParse"/> returns null for them — they're counted and skipped, not applied.
/// </summary>
public sealed class AbpRule
{
    private readonly Regex _regex;

    public bool IsException { get; }
    public bool ThirdPartyOnly { get; }
    public bool FirstPartyOnly { get; }
    public string[]? IncludeDomains { get; }   // $domain=a.com|b.com  (document must match one)
    public string[]? ExcludeDomains { get; }   // $domain=~a.com       (document must match none)

    /// <summary>Resource types this rule is scoped to (e.g. <c>$script,image</c>); null = no constraint.</summary>
    public ResourceType? IncludeTypes { get; }
    /// <summary>Resource types this rule explicitly excludes (e.g. <c>$~document</c>); null = none excluded.</summary>
    public ResourceType? ExcludeTypes { get; }

    /// <summary>A plain alnum token from the pattern used to bucket rules for fast lookup; null = general bucket.</summary>
    public string? Keyword { get; }

    /// <summary>The original filter-list line — kept for diagnostics / "why was this blocked".</summary>
    public string Source { get; private set; } = "";

    // Request-type option tokens (script, image, …) that narrow a rule to certain resource
    // kinds. Tokens with no ResourceType mapping (webrtc, object-subrequest, xbl, dtd, …) are
    // still recognized here so the rule isn't rejected outright, but they impose no type
    // constraint we can enforce — see ResourceTypeTokens for which tokens do map.
    private static readonly HashSet<string> TypeOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "image", "img", "stylesheet", "css", "xmlhttprequest", "xhr",
        "subdocument", "subdoc", "frame", "document", "doc", "media", "font",
        "object", "object-subrequest", "ping", "beacon", "websocket", "webrtc",
        "other", "popup", "popunder", "background", "xbl", "dtd", "all", "main-frame",
    };

    private AbpRule(Regex regex, bool isException, bool thirdParty, bool firstParty,
                    string[]? include, string[]? exclude, string? keyword,
                    ResourceType? includeTypes, ResourceType? excludeTypes)
    {
        _regex = regex;
        IsException = isException;
        ThirdPartyOnly = thirdParty;
        FirstPartyOnly = firstParty;
        IncludeDomains = include;
        ExcludeDomains = exclude;
        Keyword = keyword;
        IncludeTypes = includeTypes;
        ExcludeTypes = excludeTypes;
    }

    /// <summary>Parse one filter-list line. Returns null for comments, cosmetic rules,
    /// modifier-only rules, and anything we don't support.</summary>
    public static AbpRule? TryParse(string raw)
    {
        var line = raw.Trim();
        if (line.Length == 0) return null;
        if (line[0] is '!' or '[') return null;                      // comment / list header
        if (line.Contains("##") || line.Contains("#@#") ||
            line.Contains("#?#") || line.Contains("#$#")) return null; // cosmetic / scriptlet

        bool isException = false;
        if (line.StartsWith("@@", StringComparison.Ordinal))
        {
            isException = true;
            line = line[2..];
        }

        // Split off options after the last unescaped '$'.
        string pattern = line;
        string? options = null;
        int dollar = line.LastIndexOf('$');
        if (dollar >= 0)
        {
            pattern = line[..dollar];
            options = line[(dollar + 1)..];
        }

        bool thirdParty = false, firstParty = false;
        string[]? include = null, exclude = null;
        ResourceType? includeTypes = null, excludeTypes = null;

        if (options is { Length: > 0 })
        {
            foreach (var opt in options.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var o = opt.Trim();
                if (o is "third-party" or "3p") thirdParty = true;
                else if (o is "~third-party" or "~3p" or "first-party" or "1p") firstParty = true;
                else if (o.StartsWith("domain=", StringComparison.OrdinalIgnoreCase))
                {
                    var inc = new List<string>();
                    var exc = new List<string>();
                    foreach (var d in o[7..].Split('|', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (d.StartsWith('~')) exc.Add(d[1..].ToLowerInvariant());
                        else inc.Add(d.ToLowerInvariant());
                    }
                    if (inc.Count > 0) include = inc.ToArray();
                    if (exc.Count > 0) exclude = exc.ToArray();
                }
                else
                {
                    // Strip a leading ~ and any =value to get the bare option name.
                    bool negated = o.StartsWith('~');
                    var name = o.TrimStart('~');
                    int eq = name.IndexOf('=');
                    if (eq >= 0) name = name[..eq];

                    if (TypeOptions.Contains(name))
                    {
                        // A mapped token narrows/excludes the rule to specific ResourceTypes;
                        // an unmapped one (webrtc, xbl, …) is accepted but adds no constraint.
                        var mapped = ResourceTypeTokens.FromAbpToken(name);
                        if (mapped is { } t)
                        {
                            if (negated) excludeTypes = (excludeTypes ?? ResourceType.None) | t;
                            else includeTypes = (includeTypes ?? ResourceType.None) | t;
                        }
                        continue;
                    }

                    // ANYTHING ELSE ($csp, $removeparam, $redirect, $to=, $from=, $ipaddress=,
                    // $header=, $method=, …) carries a constraint we don't implement. Applying
                    // the rule without it would be WRONG in both directions: a block rule would
                    // over-block, and an @@ exception would over-allow (un-blocking real ads).
                    // The only safe choice is to drop the whole rule.
                    return null;
                }
            }
        }

        if (pattern.Length == 0) return null;

        // Reject catch-all patterns ("*", "", "|*|", "^") that would match every request.
        // A real filter list only uses these WITH options that scope them (which we've already
        // rejected above); a bare one here is a parse artefact, not a usable block rule.
        var bare = pattern.Trim('|', '*', '^');
        if (bare.Length == 0) return null;

        var (regex, keyword) = BuildRegex(pattern);
        if (regex is null) return null;

        return new AbpRule(regex, isException, thirdParty, firstParty, include, exclude, keyword,
                            includeTypes, excludeTypes)
        {
            Source = raw.Trim(),
        };
    }

    /// <summary>Does this rule apply to the given request? <paramref name="resourceType"/> is
    /// <see cref="ResourceType.None"/> when the caller has no type signal (e.g. offline testing) —
    /// treated as "unknown", so type-scoped rules still match rather than silently never firing.</summary>
    public bool Matches(string url, bool isThirdParty, string documentDomain, ResourceType resourceType = ResourceType.None)
    {
        if (ThirdPartyOnly && !isThirdParty) return false;
        if (FirstPartyOnly && isThirdParty) return false;

        if (IncludeDomains is not null && !DomainInList(documentDomain, IncludeDomains)) return false;
        if (ExcludeDomains is not null && DomainInList(documentDomain, ExcludeDomains)) return false;

        if (resourceType != ResourceType.None)
        {
            if (IncludeTypes is { } inc && (inc & resourceType) == 0) return false;
            if (ExcludeTypes is { } exc && (exc & resourceType) != 0) return false;
        }

        return _regex.IsMatch(url);
    }

    private static bool DomainInList(string documentDomain, string[] list)
    {
        foreach (var d in list)
            if (documentDomain == d || documentDomain.EndsWith("." + d, StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>Translate an ABP URL pattern into a .NET regex, and pick a bucketing keyword.</summary>
    private static (Regex? regex, string? keyword) BuildRegex(string pattern)
    {
        try
        {
            // Literal regex rule: /.../  →  use as-is.
            if (pattern.Length >= 2 && pattern[0] == '/' && pattern[^1] == '/')
            {
                var re = new Regex(pattern[1..^1], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                return (re, ExtractKeyword(pattern[1..^1]));
            }

            var sb = new StringBuilder();
            int i = 0;

            if (pattern.StartsWith("||", StringComparison.Ordinal))
            {
                // Anchor to the domain, allowing any scheme and any subdomain prefix.
                sb.Append(@"^[a-z][a-z0-9+.\-]*:\/\/(?:[^\/?#]+\.)?");
                i = 2;
            }
            else if (pattern[0] == '|')
            {
                sb.Append('^');
                i = 1;
            }

            for (; i < pattern.Length; i++)
            {
                char c = pattern[i];
                switch (c)
                {
                    case '*': sb.Append(".*"); break;
                    case '^': sb.Append(@"(?:[^\w.%\-]|$)"); break;   // ABP separator
                    case '|':
                        if (i == pattern.Length - 1) sb.Append('$');
                        else sb.Append(Regex.Escape("|"));
                        break;
                    default: sb.Append(Regex.Escape(c.ToString())); break;
                }
            }

            var regex = new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return (regex, ExtractKeyword(pattern));
        }
        catch (RegexParseException)
        {
            return (null, null);   // malformed rule — skip it
        }
    }

    // Common tokens that make useless (over-broad) buckets.
    private static readonly HashSet<string> StopTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "www", "com", "net", "org", "html", "php",
    };

    /// <summary>Pick the longest alnum run (≥3 chars, not a stop-token) to bucket the rule under.</summary>
    private static string? ExtractKeyword(string pattern)
    {
        string? best = null;
        int start = -1;
        for (int i = 0; i <= pattern.Length; i++)
        {
            bool alnum = i < pattern.Length && (char.IsLetterOrDigit(pattern[i]));
            if (alnum && start < 0) start = i;
            else if (!alnum && start >= 0)
            {
                var tok = pattern[start..i].ToLowerInvariant();
                if (tok.Length >= 3 && !StopTokens.Contains(tok) &&
                    (best is null || tok.Length > best.Length))
                    best = tok;
                start = -1;
            }
        }
        return best;
    }
}
