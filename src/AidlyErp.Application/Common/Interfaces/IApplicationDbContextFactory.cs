namespace AidlyErp.Application.Common.Interfaces;

/// <summary>
/// Creates an <see cref="IApplicationDbContext"/> in its own DI scope, and therefore its own
/// transaction. This is the .NET stand-in for Spring's
/// <c>@Transactional(propagation = REQUIRES_NEW)</c>: work done through the returned context
/// commits independently of whatever transaction the caller is already inside.
///
/// <para>Used by audit-style writes (login attempts, request logs) that must survive a rollback
/// of the operation they are recording.</para>
/// </summary>
public interface IApplicationDbContextFactory
{
    IApplicationDbContextScope CreateScope();
}

/// <summary>A disposable DI scope owning one <see cref="IApplicationDbContext"/>.</summary>
public interface IApplicationDbContextScope : IAsyncDisposable, IDisposable
{
    IApplicationDbContext Context { get; }
}
