using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Shared.Core.Audit;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Application.Services;

public interface ISys1105Service
{
    Task<List<Sys1105AuditLogDto>> SearchAsync(string? tableName, string? actionType, long? userNo,
                                               DateTime? fromAt, DateTime? toAt, int? limit,
                                               CancellationToken cancellationToken = default);
}

/// <summary>SYS1105 Activity Log Viewer — read-only, filtered view over <c>sys_audit_log</c>.</summary>
public class Sys1105Service : ISys1105Service
{
    /// <summary>Hard ceiling on rows returned, regardless of the requested limit.</summary>
    private const int MaxRows = 500;

    /// <summary>Applied when no usable limit is supplied.</summary>
    private const int DefaultRows = 200;

    private readonly ISysDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Sys1105Service(ISysDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    /// <summary>Filtered, most-recent-first audit search. Null/blank filters are ignored.</summary>
    public async Task<List<Sys1105AuditLogDto>> SearchAsync(string? tableName, string? actionType, long? userNo,
                                                            DateTime? fromAt, DateTime? toAt, int? limit,
                                                            CancellationToken cancellationToken = default)
    {
        var companyNo = _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

        var cap = limit is > 0 and <= MaxRows ? limit.Value : DefaultRows;

        var table = BlankToNull(tableName);
        var action = BlankToNull(actionType);

        var query = _db.SysAuditLogs
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo);

        if (table != null) query = query.Where(a => a.TableName == table);
        if (action != null) query = query.Where(a => a.ActionType == action);
        if (userNo != null) query = query.Where(a => a.UserNo == userNo);
        if (fromAt != null) query = query.Where(a => a.ActionAt >= fromAt);
        if (toAt != null) query = query.Where(a => a.ActionAt <= toAt);

        var rows = await query
            .OrderByDescending(a => a.ActionAt)
            .Take(cap)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    private static string? BlankToNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static Sys1105AuditLogDto ToDto(SysAuditLog a) => new()
    {
        AuditLogNo = a.AuditLogNo,
        BranchNo = a.BranchNo,
        UserNo = a.UserNo,
        SessionNo = a.SessionNo,
        TableName = a.TableName,
        RecordPk = a.RecordPk,
        ActionType = a.ActionType,
        IpAddress = a.IpAddress,
        ActionAt = a.ActionAt
    };
}
