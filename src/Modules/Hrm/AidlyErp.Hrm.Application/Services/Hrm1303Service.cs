using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Hrm.Domain;

namespace AidlyErp.Hrm.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1303Service — Leave Balance Query
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1303Service
{
    Task<List<HrmLeaveBalance>> GetBalancesAsync(long employeeNo, int? year = null, CancellationToken ct = default);
    Task<List<HrmLeaveBalance>> GetAllBalancesAsync(int? year = null, CancellationToken ct = default);
}

public class Hrm1303Service : IHrm1303Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Hrm1303Service(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<HrmLeaveBalance>> GetBalancesAsync(long employeeNo, int? year = null, CancellationToken ct = default)
    {
        int leaveYear = year ?? DateTime.UtcNow.Year;
        long? branchNo = _ctx.BranchNo;
        var query = _db.HrmLeaveBalances.AsNoTracking()
            .Where(x => x.EmployeeNo == employeeNo && x.LeaveYear == leaveYear && x.IsDeleted == 0);
        if (branchNo.HasValue)
            query = query.Where(x => x.BranchNo == branchNo.Value);
        return await query.OrderBy(x => x.EmployeeNo).ToListAsync(ct);
    }

    public async Task<List<HrmLeaveBalance>> GetAllBalancesAsync(int? year = null, CancellationToken ct = default)
    {
        int leaveYear = year ?? DateTime.UtcNow.Year;
        long? branchNo = _ctx.BranchNo;
        var query = _db.HrmLeaveBalances.AsNoTracking()
            .Where(x => x.LeaveYear == leaveYear && x.IsDeleted == 0);
        if (branchNo.HasValue)
            query = query.Where(x => x.BranchNo == branchNo.Value);
        return await query.OrderBy(x => x.EmployeeNo).ToListAsync(ct);
    }
}
