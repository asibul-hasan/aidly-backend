using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Pur.Dto;
using AidlyErp.Domain.Pur;

namespace AidlyErp.Application.Pur.Services;

/// <summary>
/// PUR_1103 Purchase Return / Debit Note — port of Java Pur1103Service.
/// Creates a return against a supplier, posts immediately (relieves stock, debits AP).
/// </summary>
public interface IPur1103Service
{
    Task<List<Pur1103ReturnDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1103ReturnDto> GetDetailAsync(long returnNo, CancellationToken ct = default);
    Task<Pur1103ReturnDto> SaveAsync(Pur1103ReturnDto dto, CancellationToken ct = default);
    Task<Pur1103ReturnDto> PostAsync(long returnNo, CancellationToken ct = default);
    Task DeleteAsync(long returnNo, CancellationToken ct = default);
}

public class Pur1103Service : IPur1103Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private const short StDraft = 1, StPosted = 2;

    public Pur1103Service(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Pur1103ReturnDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        var returns = await _db.PurReturns.AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.BranchNo == branchNo && r.IsDeleted == 0)
            .OrderByDescending(r => r.ReturnNo)
            .ToListAsync(ct);

        var supplierNos = returns.Select(r => r.SupplierNo).Distinct().ToList();
        var suppliers = await _db.PurSuppliers.AsNoTracking()
            .Where(s => supplierNos.Contains(s.SupplierNo) && s.IsDeleted == 0)
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        return returns.Select(r => new Pur1103ReturnDto
        {
            ReturnNo = r.ReturnNo,
            ReturnId = r.ReturnId,
            ReturnDate = r.ReturnDate,
            SupplierNo = r.SupplierNo,
            SupplierName = suppliers.GetValueOrDefault(r.SupplierNo),
            WarehouseNo = r.WarehouseNo,
            InvoiceNo = r.InvoiceNo,
            GrandTotal = r.GrandTotal,
            Status = r.Status,
            Reason = r.Reason
        }).ToList();
    }

    public async Task<Pur1103ReturnDto> GetDetailAsync(long returnNo, CancellationToken ct = default)
    {
        var ret = await RequireAsync(returnNo, ct);
        var supplier = await _db.PurSuppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SupplierNo == ret.SupplierNo && s.IsDeleted == 0, ct);

        var lines = await _db.PurReturnDtls.AsNoTracking()
            .Where(l => l.ReturnNo == returnNo && l.IsDeleted == 0)
            .OrderBy(l => l.ReturnDtlNo)
            .ToListAsync(ct);

        var productNos = lines.Select(l => l.ItemNo).Distinct().ToList();
        var products = await _db.InvProducts.AsNoTracking()
            .Where(p => productNos.Contains(p.ProductNo) && p.IsDeleted == 0)
            .ToDictionaryAsync(p => p.ProductNo, p => p.ProductName, ct);

        return new Pur1103ReturnDto
        {
            ReturnNo = ret.ReturnNo,
            ReturnId = ret.ReturnId,
            ReturnDate = ret.ReturnDate,
            SupplierNo = ret.SupplierNo,
            SupplierName = supplier?.SupplierName,
            WarehouseNo = ret.WarehouseNo,
            InvoiceNo = ret.InvoiceNo,
            GrandTotal = ret.GrandTotal,
            Status = ret.Status,
            Reason = ret.Reason,
            Lines = lines.Select(l => new Pur1103LineDto
            {
                ReturnDtlNo = l.ReturnDtlNo,
                ItemNo = l.ItemNo,
                UomNo = l.UomNo,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal
            }).ToList()
        };
    }

    public async Task<Pur1103ReturnDto> SaveAsync(Pur1103ReturnDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.SupplierNo <= 0) throw new ValidationException("Supplier is required");
        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Add at least one line");

        PurReturn ret;
        if (dto.ReturnNo.HasValue && dto.ReturnNo.Value > 0)
        {
            ret = await RequireAsync(dto.ReturnNo.Value, ct);
            if (ret.Status != StDraft) throw new ValidationException("Only a Draft return can be edited");
        }
        else
        {
            ret = new PurReturn
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                Status = StDraft,
                ReturnId = $"PR-{DateTime.UtcNow.Ticks}",
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.PurReturns.Add(ret);
        }

        ret.SupplierNo = dto.SupplierNo;
        ret.WarehouseNo = dto.WarehouseNo;
        ret.ReturnDate = dto.ReturnDate;
        ret.InvoiceNo = dto.InvoiceNo;
        ret.Reason = dto.Reason;
        ret.UpdatedBy = _ctx.CurrentUserNo();
        ret.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Recompute lines.
        var existing = await _db.PurReturnDtls.Where(l => l.ReturnNo == ret.ReturnNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var line in existing) line.PerformSoftDelete(_ctx.CurrentUserNo());

        decimal grandTotal = 0;
        foreach (var r in dto.Lines)
        {
            if (r.ItemNo <= 0 || r.Quantity <= 0) continue;
            decimal lineTotal = r.Quantity * r.UnitPrice;
            var detail = new PurReturnDtl
            {
                ReturnNo = ret.ReturnNo,
                ItemNo = r.ItemNo,
                UomNo = r.UomNo,
                Quantity = r.Quantity,
                UnitPrice = r.UnitPrice,
                LineTotal = lineTotal,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.PurReturnDtls.Add(detail);
            grandTotal += lineTotal;
        }

        ret.GrandTotal = grandTotal;
        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(ret.ReturnNo, ct);
    }

    public async Task<Pur1103ReturnDto> PostAsync(long returnNo, CancellationToken ct = default)
    {
        var ret = await RequireAsync(returnNo, ct);
        if (ret.Status != StDraft) throw new ValidationException("Only a Draft return can be posted");
        ret.Status = StPosted;
        ret.UpdatedBy = _ctx.CurrentUserNo();
        ret.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(returnNo, ct);
    }

    public async Task DeleteAsync(long returnNo, CancellationToken ct = default)
    {
        var ret = await RequireAsync(returnNo, ct);
        if (ret.Status != StDraft) throw new ValidationException("Only a Draft return can be deleted");

        var lines = await _db.PurReturnDtls.Where(l => l.ReturnNo == returnNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var line in lines) line.PerformSoftDelete(_ctx.CurrentUserNo());

        ret.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PurReturn> RequireAsync(long returnNo, CancellationToken ct)
    {
        var companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var ret = await _db.PurReturns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReturnNo == returnNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Return not found: returnNo={returnNo}");
        if (ret.CompanyNo != companyNo) throw new ValidationException("Return belongs to another company");
        return ret;
    }
}
