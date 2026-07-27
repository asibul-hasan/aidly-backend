using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Fin.Dto;
using AidlyErp.Domain.Fin;

namespace AidlyErp.Application.Fin.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Fin1308ChartOfAccountsPdfService — COA Export
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1308ChartOfAccountsPdfService
{
    Task<List<Fin1308ChartOfAccountsRowDto>> GetCoaExportRowsAsync(CancellationToken ct = default);
}

public class Fin1308ChartOfAccountsPdfService : IFin1308ChartOfAccountsPdfService
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Fin1308ChartOfAccountsPdfService(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1308ChartOfAccountsRowDto>> GetCoaExportRowsAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var groups = await _db.FinAccountGroups.AsNoTracking().Where(g => g.IsDeleted == 0).ToDictionaryAsync(g => g.AccountGroupNo, g => g.GroupName, ct);

        return await _db.FinAccounts
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0)
            .OrderBy(a => a.AccountCode)
            .Select(a => new Fin1308ChartOfAccountsRowDto
            {
                AccountNo = a.AccountNo,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                GroupName = groups.ContainsKey(a.AccountGroupNo) ? groups[a.AccountGroupNo] : null,
                RootTypeName = RootTypeName(a.RootType),
                NormalBalance = a.NormalBalance,
                IsPostable = a.IsPostable
            })
            .ToListAsync(ct);
    }

    private static string RootTypeName(short rootType) => rootType switch
    {
        1 => "Asset",
        2 => "Liability",
        3 => "Equity",
        4 => "Revenue",
        5 => "Expense",
        _ => "Other"
    };
}
