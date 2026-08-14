using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Interfaces;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Pur.Application.Services;

/// <summary>
/// Dropdown data shared by the PUR document forms. PUR_1102 (invoice) and PUR_1103 (return) ask for
/// the same four lists, so they resolve through one service rather than duplicating the queries.
/// </summary>
public interface IPurLookupService
{
    Task<Pur1102LookupDto> GetLookupsAsync(CancellationToken ct = default);
}

public class PurLookupService : IPurLookupService
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvLookup _invLookup;

    public PurLookupService(IPurDbContext db, ICompanyBranchContext ctx, IInvLookup invLookup)
    {
        _db = db;
        _ctx = ctx;
        _invLookup = invLookup;
    }

    public async Task<Pur1102LookupDto> GetLookupsAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        // Suppliers are PUR's own master; warehouses/products/UOMs come from INV through the shared
        // lookup contract, the same way SAL_1001 resolves them.
        var suppliers = await _db.PurSuppliers.AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == 0 && (s.IsActive == null || s.IsActive == 1))
            .OrderBy(s => s.SupplierNo)
            .Select(s => new PurOptionDto(s.SupplierNo, s.SupplierId + " — " + s.SupplierName))
            .ToListAsync(ct);

        var warehouses = await _invLookup.GetWarehousesAsync(companyNo, branchNo, ct);
        var products = await _invLookup.GetProductsAsync(companyNo, ct);
        var uoms = await _invLookup.GetUomsAsync(companyNo, ct);

        return new Pur1102LookupDto
        {
            Suppliers = suppliers,
            Warehouses = warehouses.Select(w => new PurOptionDto(w.No, w.Name)).ToList(),
            Products = products.Select(p => new PurOptionDto(p.No, p.Name)).ToList(),
            Uoms = uoms.Select(u => new PurOptionDto(u.No, u.Name)).ToList()
        };
    }
}
