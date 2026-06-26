using Microsoft.Extensions.Options;

namespace MoviesMafia.Services.Streaming;

/// <summary>
/// Default <see cref="IProviderCatalog"/>. Seeds from the inline <c>Streaming:Providers</c> config
/// so the app always has a working list immediately, even before any remote fetch completes. The
/// active list is swapped atomically via a <c>volatile</c> reference, so concurrent SSR reads never
/// see a torn list.
/// </summary>
public sealed class ProviderCatalog : IProviderCatalog
{
    private volatile IReadOnlyList<StreamingProvider> _providers;

    public ProviderCatalog(IOptions<StreamingOptions> options) =>
        _providers = options.Value.Providers ?? new List<StreamingProvider>();

    public IReadOnlyList<StreamingProvider> Providers => _providers;

    public void Set(IReadOnlyList<StreamingProvider> providers)
    {
        if (providers is { Count: > 0 }) _providers = providers;
    }
}
