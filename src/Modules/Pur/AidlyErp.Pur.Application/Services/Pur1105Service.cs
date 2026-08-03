using AidlyErp.Pur.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Domain;

namespace AidlyErp.Pur.Application.Services;

/// <summary>
/// PUR_1105 Goods Receipt Note — port of Java Pur1105Service.
/// Receives goods against a supplier (optionally against a PO), posts immediately.
/// </summary>
public interface IPur1105Service
{
    Task<List<Pur1105ReceiptDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1105ReceiptDto> GetDetailAsync(long receiptNo, CancellationToken ct = default);
    Task<Pur1105ReceiptDto> SaveAsync(Pur1105ReceiptDto dto, CancellationToken ct = default);
    Task<Pur1105ReceiptDto> PostAsync(long receiptNo, CancellationToken ct = default);
    Task DeleteAsync(long receiptNo, CancellationToken ct = default);
}

public class Pur1105Service : IPur1105Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private const short StDraft = 1, StPosted = 2;

    public Pur1105Service(IPurDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Pur1105ReceiptDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        return await _db.PurReceipts.AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.BranchNo == branchNo && r.IsDeleted == 0)
            .OrderByDescending(r => r.ReceiptNo)
            .Select(r => new Pur1105ReceiptDto
            {
                ReceiptNo = r.ReceiptNo,
                ReceiptId = r.ReceiptId,
                ReceiptDate = r.ReceiptDate,
                SupplierNo = r.SupplierNo,
                WarehouseNo = r.WarehouseNo,
                Status = r.Status
            })
            .ToListAsync(ct);
    }

    public async Task<Pur1105ReceiptDto> GetDetailAsync(long receiptNo, CancellationToken ct = default)
    {
        var receipt = await RequireAsync(receiptNo, ct);

        var lines = await _db.PurReceiptDtls.AsNoTracking()
            .Where(l => l.ReceiptNo == receiptNo && l.IsDeleted == 0)
            .OrderBy(l => l.ReceiptDtlNo)
            .ToListAsync(ct);

        return new Pur1105ReceiptDto
        {
            ReceiptNo = receipt.ReceiptNo,
            ReceiptId = receipt.ReceiptId,
            ReceiptDate = receipt.ReceiptDate,
            SupplierNo = receipt.SupplierNo,
            WarehouseNo = receipt.WarehouseNo,
            Status = receipt.Status,
            Lines = lines.Select(l => new Pur1105LineDto
            {
                ReceiptDtlNo = l.ReceiptDtlNo,
                ItemNo = l.ItemNo,
                UomNo = l.UomNo,
                Quantity = l.Quantity
            }).ToList()
        };
    }

    public async Task<Pur1105ReceiptDto> SaveAsync(Pur1105ReceiptDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.SupplierNo <= 0) throw new ValidationException("Supplier is required");
        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Add at least one line");

        PurReceipt receipt;
        if (dto.ReceiptNo.HasValue && dto.ReceiptNo.Value > 0)
        {
            receipt = await RequireAsync(dto.ReceiptNo.Value, ct);
            if (receipt.Status != StDraft) throw new ValidationException("Only a Draft receipt can be edited");
        }
        else
        {
            receipt = new PurReceipt
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                Status = StDraft,
                ReceiptId = $"GRN-{DateTime.UtcNow.Ticks}",
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.PurReceipts.Add(receipt);
        }

        receipt.SupplierNo = dto.SupplierNo;
        receipt.WarehouseNo = dto.WarehouseNo;
        receipt.ReceiptDate = dto.ReceiptDate;
        receipt.UpdatedBy = _ctx.CurrentUserNo();
        receipt.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Recompute lines.
        var existing = await _db.PurReceiptDtls.Where(l => l.ReceiptNo == receipt.ReceiptNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var line in existing) line.PerformSoftDelete(_ctx.CurrentUserNo());

        foreach (var r in dto.Lines)
        {
            if (r.ItemNo <= 0 || r.Quantity <= 0) continue;
            var detail = new PurReceiptDtl
            {
                ReceiptNo = receipt.ReceiptNo,
                ItemNo = r.ItemNo,
                UomNo = r.UomNo,
                Quantity = r.Quantity,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.PurReceiptDtls.Add(detail);
        }

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(receipt.ReceiptNo, ct);
    }

    public async Task<Pur1105ReceiptDto> PostAsync(long receiptNo, CancellationToken ct = default)
    {
        var receipt = await RequireAsync(receiptNo, ct);
        if (receipt.Status != StDraft) throw new ValidationException("Only a Draft receipt can be posted");
        receipt.Status = StPosted;
        receipt.UpdatedBy = _ctx.CurrentUserNo();
        receipt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(receiptNo, ct);
    }

    public async Task DeleteAsync(long receiptNo, CancellationToken ct = default)
    {
        var receipt = await RequireAsync(receiptNo, ct);
        if (receipt.Status != StDraft) throw new ValidationException("Only a Draft receipt can be deleted");

        var lines = await _db.PurReceiptDtls.Where(l => l.ReceiptNo == receiptNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var line in lines) line.PerformSoftDelete(_ctx.CurrentUserNo());

        receipt.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PurReceipt> RequireAsync(long receiptNo, CancellationToken ct)
    {
        var companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var receipt = await _db.PurReceipts.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReceiptNo == receiptNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Receipt not found: receiptNo={receiptNo}");
        if (receipt.CompanyNo != companyNo) throw new ValidationException("Receipt belongs to another company");
        return receipt;
    }
}
