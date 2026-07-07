namespace AdBlockCore;

/// <summary>
/// Portable mirror of the resource-type categories a host can observe for an outgoing
/// request (WebView2's <c>CoreWebView2WebResourceContext</c>, a browser extension's
/// <c>webRequest</c> type, etc.). Kept host-agnostic on purpose — this library must not
/// reference WebView2 types (see HANDOFF's cross-platform note) — so the WPF shell maps
/// <c>CoreWebView2WebResourceContext</c> onto this enum at the call site.
/// </summary>
[Flags]
public enum ResourceType
{
    None = 0,
    Document = 1 << 0,
    Stylesheet = 1 << 1,
    Image = 1 << 2,
    Media = 1 << 3,
    Font = 1 << 4,
    Script = 1 << 5,
    XmlHttpRequest = 1 << 6,
    WebSocket = 1 << 7,
    Ping = 1 << 8,
    Other = 1 << 9,

    All = Document | Stylesheet | Image | Media | Font | Script | XmlHttpRequest | WebSocket | Ping | Other,
}

/// <summary>Maps Adblock Plus / EasyList type-option tokens (the <c>$script</c>, <c>$image</c>, …
/// modifiers) onto <see cref="ResourceType"/>. Tokens with no WebView2-observable equivalent
/// (<c>popup</c>, <c>popunder</c>, <c>xbl</c>, <c>dtd</c>, <c>webrtc</c>, <c>object</c>,
/// <c>object-subrequest</c>) are intentionally unmapped — they narrow on a dimension we have no
/// signal for, so a rule bearing ONLY those is left unconstrained rather than guessed at.</summary>
public static class ResourceTypeTokens
{
    public static ResourceType? FromAbpToken(string token) => token switch
    {
        "script" => ResourceType.Script,
        "image" or "img" => ResourceType.Image,
        "stylesheet" or "css" => ResourceType.Stylesheet,
        "xmlhttprequest" or "xhr" => ResourceType.XmlHttpRequest,
        // ABP's "subdocument" (iframe navigations) and "document"/"main-frame" both land on
        // WebView2's single Document context — it doesn't distinguish top frame from a
        // subframe's own navigation request the way ABP's type list does.
        "subdocument" or "subdoc" or "frame" or "document" or "doc" or "main-frame" => ResourceType.Document,
        "media" => ResourceType.Media,
        "font" => ResourceType.Font,
        "ping" or "beacon" => ResourceType.Ping,
        "websocket" => ResourceType.WebSocket,
        "other" or "background" => ResourceType.Other,
        "all" => ResourceType.All,
        _ => null,
    };
}
