using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

/// <summary>
/// The warehouse / product / UOM option lists every INV document form fills its dropdowns from.
/// INV owns these tables, so unlike PUR and SAL it reads them directly rather than through
/// <c>IInvLookup</c>.
/// </summary>
public interface IInvLookupService
{
    Task<List<InvOptionDto>> GetWarehouseOptionsAsync(CancellationToken ct = default);
    Task<List<InvOptionDto>> GetProductOptionsAsync(CancellationToken ct = default);
    Task<List<InvOptionDto>> GetUomOptionsAsync(CancellationToken ct = default);
}

public class InvLookupService : IInvLookupService
{
    private const short Deleted = 0;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public InvLookupService(IInvDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    /// <summary>Warehouses are branch-scoped: a user only transacts against their own branch's stores.</summary>
    public async Task<List<InvOptionDto>> GetWarehouseOptionsAsync(CancellationToken ct = default)
    {
        long companyNo = Company();
        long branchNo = Branch();

        return await _db.InvWarehouses.AsNoTracking()
            .Where(w => w.CompanyNo == companyNo && w.BranchNo == branchNo && w.IsDeleted == Deleted)
            .OrderBy(w => w.WarehouseNo)
            .Select(w => new InvOptionDto(w.WarehouseNo, w.WarehouseName))
            .ToListAsync(ct);
    }

    public async Task<List<InvOptionDto>> GetProductOptionsAsync(CancellationToken ct = default)
    {
        long companyNo = Company();

        return await _db.InvProducts.AsNoTracking()
            .Where(p => p.CompanyNo == companyNo && p.IsDeleted == Deleted)
            .OrderBy(p => p.ProductNo)
            .Select(p => new InvOptionDto(p.ProductNo, p.ProductId + " — " + p.ProductName))
            .ToListAsync(ct);
    }

    public async Task<List<InvOptionDto>> GetUomOptionsAsync(CancellationToken ct = default)
    {
        long companyNo = Company();

        return await _db.InvUoms.AsNoTracking()
            .Where(u => u.CompanyNo == companyNo && u.IsDeleted == Deleted)
            .OrderBy(u => u.UomNo)
            .Select(u => new InvOptionDto(u.UomNo, u.UomName))
            .ToListAsync(ct);
    }

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("No active branch in context");
}
