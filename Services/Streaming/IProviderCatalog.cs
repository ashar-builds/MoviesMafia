namespace MoviesMafia.Services.Streaming;

/// <summary>
/// Holds the current list of streaming providers in memory. The list may come from a remote URL
/// (refreshed in the background) or from the inline config fallback. Reads are synchronous so the
/// embed-URL builder can be used from the Static SSR render path.
/// </summary>
public interface IProviderCatalog
{
    /// <summary>The providers currently in effect. Never null; never throws.</summary>
    IReadOnlyList<StreamingProvider> Providers { get; }

    /// <summary>Atomically replaces the active provider list (used by the background refresher).</summary>
    void Set(IReadOnlyList<StreamingProvider> providers);
}
