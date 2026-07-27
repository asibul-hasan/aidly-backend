using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Domain.Hrm;

namespace AidlyErp.Application.Hrm.Services;

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
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Hrm1303Service(IApplicationDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

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
