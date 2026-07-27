using AidlyErp.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Infrastructure.Persistence;

/// <inheritdoc cref="IApplicationDbContextFactory"/>
public sealed class ApplicationDbContextFactory : IApplicationDbContextFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ApplicationDbContextFactory(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public IApplicationDbContextScope CreateScope() => new Scope(_scopeFactory.CreateScope());

    private sealed class Scope : IApplicationDbContextScope
    {
        private readonly IServiceScope _scope;

        public Scope(IServiceScope scope)
        {
            _scope = scope;
            Context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        }

        public IApplicationDbContext Context { get; }

        public void Dispose() => _scope.Dispose();

        public ValueTask DisposeAsync()
        {
            if (_scope is IAsyncDisposable asyncScope) return asyncScope.DisposeAsync();
            _scope.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
