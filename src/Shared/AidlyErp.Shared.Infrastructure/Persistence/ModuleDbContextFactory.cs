using AidlyErp.Shared.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Shared.Infrastructure.Persistence;

/// <summary>
/// Resolves a module context from a fresh DI scope so it gets its own <c>DbContext</c> instance —
/// and therefore its own transaction, independent of the caller's.
/// </summary>
public sealed class ModuleDbContextFactory<TContext> : IModuleDbContextFactory<TContext>
    where TContext : notnull
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ModuleDbContextFactory(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public IModuleDbContextScope<TContext> CreateScope() => new Scope(_scopeFactory.CreateScope());

    private sealed class Scope : IModuleDbContextScope<TContext>
    {
        private readonly IServiceScope _scope;

        public Scope(IServiceScope scope) => _scope = scope;

        public TContext Context => _scope.ServiceProvider.GetRequiredService<TContext>();

        public void Dispose() => _scope.Dispose();

        public ValueTask DisposeAsync() =>
            _scope is IAsyncDisposable async ? async.DisposeAsync() : Disposing();

        private ValueTask Disposing()
        {
            _scope.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
