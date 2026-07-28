namespace AidlyErp.Shared.Core.Exceptions;

/// <summary>
/// Root of the application's business-error hierarchy. Mirrors the Java
/// <c>core.shared.exception.DomainException</c> (a RuntimeException), so a single
/// <c>catch (DomainException)</c> covers not-found and validation failures too.
/// Handled as HTTP 500 unless a more specific subclass matches.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception cause) : base(message, cause) { }
}

/// <summary>Requested resource does not exist. Handled as HTTP 404.</summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' ({key}) was not found.") { }
}

/// <summary>Business-rule / input validation failure. Handled as HTTP 400.</summary>
public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message) { }
}
