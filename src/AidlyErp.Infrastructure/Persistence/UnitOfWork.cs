using AidlyErp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>. Scoped per request, so the ambient
/// transaction is shared by every service resolved for that request — the same reach a Spring
/// <c>@Transactional</c> boundary has.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UnitOfWork> _logger;

    /// <summary>
    /// Set while an unfiltered block is running. <see cref="ApplicationDbContext"/> reads this to
    /// bypass the tenant and soft-delete global query filters.
    /// </summary>
    private bool _unfiltered;

    public UnitOfWork(ApplicationDbContext db, ILogger<UnitOfWork> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
                                                     CancellationToken cancellationToken = default)
    {
        // Propagation.REQUIRED — join the ambient transaction rather than nesting a second one.
        if (_db.Database.CurrentTransaction is not null)
        {
            return await work(cancellationToken);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await work(cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction rolled back: {Message}", ex.Message);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        await ExecuteAsync<object?>(async ct =>
        {
            await work(ct);
            return null;
        }, cancellationToken);

    public async Task<TResult> RunUnfilteredAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
                                                           CancellationToken cancellationToken = default)
    {
        var previous = _unfiltered;
        _unfiltered = true;
        _db.SuppressQueryFilters = true;
        try
        {
            return await work(cancellationToken);
        }
        finally
        {
            _unfiltered = previous;
            _db.SuppressQueryFilters = previous;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
