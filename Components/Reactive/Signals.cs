using ReactiveBlazor;

namespace MoviesMafia.Components.Reactive;

/// <summary>Published when a user's media requests change, so subscribed components refresh out-of-band.</summary>
public sealed record MediaRequestChanged : IReactiveSignal;
