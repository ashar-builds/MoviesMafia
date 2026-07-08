using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdBlockCore;

/// <summary>
/// Turns a <see cref="CosmeticEngine"/>'s rule tables into a single self-contained JavaScript
/// payload for <c>CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync</c>.
///
/// Per Microsoft's current WebView2 docs (verified against the live API reference, not assumed):
/// that method "applies to all future top level document and child frame navigations" and runs
/// "after the global object has been created, but before the HTML document has been parsed and
/// before any other script included by the HTML document is run." That is exactly the reach we
/// need — it fires inside the cross-origin provider iframe too, before the provider's own ad
/// script can run, because WebView2 grants the HOST this hook regardless of the frame's origin
/// (same_origin policy constrains *page* JS, not the embedding host).
///
/// Design choice (HANDOFF.md offered two): rather than a postMessage round-trip (option a) or a
/// host-side-precomputed-per-origin stylesheet (impossible ahead of time — we don't know which
/// hosts will load until they do), this embeds the FULL rule tables as JSON directly in the one
/// script registered for ALL frames. Each frame resolves its own applicable selectors from
/// `location.hostname`, synchronously, with no round trip — simpler than (a) and still correct
/// for arbitrary future hosts, unlike a precomputed single-origin stylesheet. Trade-off: the
/// injected payload scales with total rule count, evaluated once per frame per navigation; see
/// README's "Known limits" for the option to switch to (a) if this profiles poorly on a page
/// with many iframes.
/// </summary>
public static class CosmeticInjector
{
    private const string StyleElementId = "__adblockshell_cosmetic__";

    public static string BuildScript(CosmeticEngine engine, IReadOnlyCollection<string> allowlistedHosts)
    {
        var data = new PayloadData(
            Generic: engine.GenericHideSelectors,
            GenericExcludedOn: engine.GenericHideExclusions,
            DomainHide: engine.DomainHideSelectors,
            DomainException: engine.DomainExceptionSelectors,
            GlobalException: engine.GlobalExceptionSelectors,
            Allowlist: allowlistedHosts
        );
        var json = JsonSerializer.Serialize(data, CosmeticJsonContext.Default.PayloadData);

        // The whole thing runs synchronously in the frame's own JS context — before the
        // frame's HTML has been parsed, so `document.documentElement` already exists (an
        // empty root the parser hasn't filled in yet) but `document.head` may not.
        return $$"""
        (function() {
            var DATA = {{json}};

            function isMatch(host, domain) {
                return host === domain || (host.length > domain.length && host.slice(-(domain.length + 1)) === "." + domain);
            }
            function anyMatch(host, list) {
                for (var i = 0; i < list.length; i++) if (isMatch(host, list[i])) return true;
                return false;
            }

            function buildCss(host) {
                if (anyMatch(host, DATA.Allowlist)) return "";

                var selectors = [];
                for (var i = 0; i < DATA.Generic.length; i++) {
                    var sel = DATA.Generic[i];
                    var excluded = false;
                    for (var domain in DATA.GenericExcludedOn) {
                        if (isMatch(host, domain) && DATA.GenericExcludedOn[domain].indexOf(sel) !== -1) { excluded = true; break; }
                    }
                    if (!excluded) selectors.push(sel);
                }
                for (var d in DATA.DomainHide) {
                    if (isMatch(host, d)) selectors = selectors.concat(DATA.DomainHide[d]);
                }
                if (selectors.length === 0) return "";

                var excepted = {};
                for (var i = 0; i < DATA.GlobalException.length; i++) excepted[DATA.GlobalException[i]] = true;
                for (var d in DATA.DomainException) {
                    if (isMatch(host, d)) for (var i = 0; i < DATA.DomainException[d].length; i++) excepted[DATA.DomainException[d][i]] = true;
                }

                var seen = {};
                var kept = [];
                for (var i = 0; i < selectors.length; i++) {
                    var s = selectors[i];
                    if (excepted[s] || seen[s]) continue;
                    seen[s] = true;
                    kept.push(s);
                }
                if (kept.length === 0) return "";
                return kept.join(", ") + " { display: none !important; }";
            }

            var css = buildCss(location.hostname);
            if (!css) return;

            function inject() {
                if (document.getElementById("{{StyleElementId}}")) return;
                var style = document.createElement("style");
                style.id = "{{StyleElementId}}";
                style.textContent = css;
                (document.head || document.documentElement).appendChild(style);
            }

            inject();

            // Some SPA frameworks replace <head> wholesale on route changes, which would drop
            // our injected <style>. A MutationObserver on documentElement re-inserts it if that
            // happens; CSS itself already applies live to elements added later, so this observer
            // exists ONLY to guard the style element's own survival, not to re-scan for ads.
            new MutationObserver(function() { inject(); })
                .observe(document.documentElement, { childList: true, subtree: true });
        })();
        """;
    }

    internal sealed record PayloadData(
        IReadOnlyCollection<string> Generic,
        IReadOnlyDictionary<string, IReadOnlyList<string>> GenericExcludedOn,
        IReadOnlyDictionary<string, IReadOnlyList<string>> DomainHide,
        IReadOnlyDictionary<string, IReadOnlyList<string>> DomainException,
        IReadOnlyCollection<string> GlobalException,
        IReadOnlyCollection<string> Allowlist);
}

/// <summary>
/// Source-generated (reflection-free) JSON metadata for the cosmetic payload — same rationale as
/// <c>AppSettingsJsonContext</c>: keeps the Android Release linker ON by removing the reflection
/// serialization the trimmer can't follow.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(CosmeticInjector.PayloadData))]
internal sealed partial class CosmeticJsonContext : JsonSerializerContext;
