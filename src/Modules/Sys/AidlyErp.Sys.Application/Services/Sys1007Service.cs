using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Application.Services;

public interface ISys1007Service
{
    Task<List<Sys1007CostCenterDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<Sys1007CostCenterDto?> SaveAsync(Sys1007CostCenterDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(long costCenterNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// SYS1007 Cost Center Setup (<c>sys_cost_center</c>) — company-scoped accounting dimension.
/// </summary>
public class Sys1007Service : ISys1007Service
{
    private const short Deleted = 0;
    private const short Active = 1;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;

    public Sys1007Service(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
    }

    /// <summary>
    /// Branch-scoped read: cost centres of the current login branch PLUS company-wide ones
    /// (<c>branch_no IS NULL</c>). Company-wide roots apply to every branch.
    /// </summary>
    public async Task<List<Sys1007CostCenterDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = Company();
        var branchNo = _ctx.BranchNo;

        var rows = await _db.CostCenters
            .AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.IsDeleted == Deleted
                        && (c.BranchNo == null || (branchNo != null && c.BranchNo == branchNo)))
            .OrderBy(c => c.CostCenterNo)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public Task<Sys1007CostCenterDto?> SaveAsync(Sys1007CostCenterDto dto,
                                                 CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct => dto.CostCenterNo != null
            ? await UpdateAsync(dto, ct)
            : await InsertAsync(dto, ct), cancellationToken);

    /// <summary>
    /// Insert. Branch handling follows the hierarchy:
    /// <list type="bullet">
    ///   <item>ROOT (no parent) → branch multi-select honoured: one record per selected branch, or
    ///         one company-wide record (<c>branch_no = NULL</c>) when empty.</item>
    ///   <item>CHILD (has parent) → INHERITS the parent's <c>branch_no</c> (single record); the
    ///         client's <c>branch_nos</c> is ignored so the whole subtree stays on one branch.</item>
    /// </list>
    /// </summary>
    private async Task<Sys1007CostCenterDto?> InsertAsync(Sys1007CostCenterDto dto, CancellationToken ct)
    {
        var companyNo = Company();
        var code = RequireCode(dto.CostCenterId);
        var parentNo = dto.ParentCostCenterNo;

        if (parentNo != null)
        {
            // Child → inherit the parent's branch
            var parent = await _db.CostCenters
                             .FirstOrDefaultAsync(c => c.CostCenterNo == parentNo && c.IsDeleted == Deleted, ct)
                         ?? throw new NotFoundException("Parent cost center not found");

            return ToDto(await SaveOneAsync(companyNo, parent.BranchNo, code, dto, parentNo, ct));
        }

        // Root → fan out across the selected branches (empty = all branches → NULL)
        var branchNos = ResolveBranches(dto.BranchNos);
        CostCenter? saved = null;

        foreach (var branchNo in branchNos)
        {
            saved = await SaveOneAsync(companyNo, branchNo, code, dto, null, ct);
        }

        return saved == null ? null : ToDto(saved);
    }

    private async Task<Sys1007CostCenterDto> UpdateAsync(Sys1007CostCenterDto dto, CancellationToken ct)
    {
        var e = await _db.CostCenters
                    .FirstOrDefaultAsync(c => c.CostCenterNo == dto.CostCenterNo && c.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Cost center not found");

        if (e.CompanyNo != Company())
        {
            throw new NotFoundException("Cost center not found");
        }

        // A cost center that has children cannot be re-parented (moved under another). Moving a
        // whole subtree would break the root-owned branch inheritance and the hierarchy, so the
        // parent is locked once it has descendants.
        var parentChanged = dto.ParentCostCenterNo != e.ParentCostCenterNo;

        if (parentChanged)
        {
            var hasChildren = await _db.CostCenters
                .AnyAsync(c => c.ParentCostCenterNo == e.CostCenterNo && c.IsDeleted == Deleted, ct);

            if (hasChildren)
            {
                throw new ValidationException(
                    "This cost center has child records and cannot be moved under another cost center.");
            }
        }

        // A cost center can never be its own parent.
        if (dto.ParentCostCenterNo != null && dto.ParentCostCenterNo == e.CostCenterNo)
        {
            throw new ValidationException("A cost center cannot be its own parent.");
        }

        var code = RequireCode(dto.CostCenterId);

        // Per-(company, branch) uniqueness on a different record.
        var clash = await _db.CostCenters
            .AnyAsync(c => c.CompanyNo == e.CompanyNo && c.IsDeleted == Deleted
                           && c.CostCenterId.ToUpper() == code
                           && c.BranchNo == e.BranchNo
                           && c.CostCenterNo != e.CostCenterNo, ct);

        if (clash)
        {
            throw new ValidationException("Cost center code already exists for this branch: " + code);
        }

        e.CostCenterId = code;
        e.CostCenterName = dto.CostCenterName ?? e.CostCenterName;
        e.ParentCostCenterNo = dto.ParentCostCenterNo;
        e.IsActive = dto.IsActive ?? Active;

        // branch_no is NOT changed on update — it's owned by the root/inheritance rule.

        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    /// <summary>Creates a single cost-centre row for the given branch with a per-branch unique code.</summary>
    private async Task<CostCenter> SaveOneAsync(long companyNo, long? branchNo, string code,
                                                Sys1007CostCenterDto dto, long? parentNo, CancellationToken ct)
    {
        var duplicate = await _db.CostCenters
            .AnyAsync(c => c.CompanyNo == companyNo && c.BranchNo == branchNo
                           && c.CostCenterId == code && c.IsDeleted == Deleted, ct);

        if (duplicate)
        {
            throw new ValidationException("Cost center code already exists for this branch: " + code);
        }

        var e = new CostCenter
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            CostCenterId = code,
            CostCenterName = dto.CostCenterName ?? string.Empty,
            ParentCostCenterNo = parentNo,
            IsActive = dto.IsActive ?? Active
        };

        _db.CostCenters.Add(e);
        await _db.SaveChangesAsync(ct);

        return e;
    }

    public Task DeleteAsync(long costCenterNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var e = await _db.CostCenters
                        .FirstOrDefaultAsync(c => c.CostCenterNo == costCenterNo && c.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Cost center not found");

            var hasChildren = await _db.CostCenters
                .AnyAsync(c => c.ParentCostCenterNo == costCenterNo && c.IsDeleted == Deleted, ct);

            if (hasChildren)
            {
                throw new ValidationException("Cannot delete: cost center has child records. Delete children first.");
            }

            e.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    // ─── Private ─────────────────────────────────────────────────────────────

    private long Company() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    private static string RequireCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException("Cost center code is required");
        }

        return code.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// <c>branch_nos</c> from the DTO: null/empty = company-wide (NULL <c>branch_no</c>);
    /// otherwise one record per branch.
    /// </summary>
    private static List<long?> ResolveBranches(List<long>? branchNos)
    {
        if (branchNos == null) return new List<long?> { null };

        var cleaned = branchNos.Distinct().Select(b => (long?)b).ToList();
        return cleaned.Count == 0 ? new List<long?> { null } : cleaned;
    }

    private static Sys1007CostCenterDto ToDto(CostCenter e) => new()
    {
        CostCenterNo = e.CostCenterNo,
        CostCenterId = e.CostCenterId,
        CostCenterName = e.CostCenterName,
        BranchNo = e.BranchNo,
        ParentCostCenterNo = e.ParentCostCenterNo,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
