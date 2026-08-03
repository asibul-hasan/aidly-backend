namespace AidlyErp.Shared.Core.Abstractions;

/// <summary>
/// Creates a module context in its own DI scope, and therefore its own transaction. This is the
/// .NET stand-in for Spring's <c>@Transactional(propagation = REQUIRES_NEW)</c>: work done through
/// the returned context commits independently of whatever transaction the caller is already inside.
///
/// <para>Used by audit-style writes — login attempts, request logs — that must survive a rollback
/// of the operation they are recording. A failed login is exactly the case that matters: the
/// business operation aborts, but the attempt still has to be counted, or lockout never triggers.</para>
/// </summary>
/// <typeparam name="TContext">The module's context interface, e.g. <c>ISysDbContext</c>.</typeparam>
public interface IModuleDbContextFactory<out TContext>
{
    IModuleDbContextScope<TContext> CreateScope();
}

/// <summary>A disposable DI scope owning one module context.</summary>
public interface IModuleDbContextScope<out TContext> : IAsyncDisposable, IDisposable
{
    TContext Context { get; }
}
