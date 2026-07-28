using AidlyErp.Sal.Application.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Application.Contract;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Domain;
using AidlyErp.Sys.Domain;

namespace AidlyErp.Sal.Application.Services;

public interface ISal1102Service
{
    Task<List<Sal1102ReceiptDto>> GetListAsync(CancellationToken ct = default);
    Task<Sal1102ReceiptDto> GetDetailAsync(long receiptNo, CancellationToken ct = default);
    Task<List<Sal1102OpenInvoiceDto>> GetOpenInvoicesAsync(long customerNo, CancellationToken ct = default);
    Task<Sal1102ReceiptDto> SaveAsync(Sal1102ReceiptDto dto, CancellationToken ct = default);
    Task<Sal1102ReceiptDto> PostAsync(long receiptNo, CancellationToken ct = default);
    Task DeleteAsync(long receiptNo, CancellationToken ct = default);
}

public class Sal1102Service : ISal1102Service
{
    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly ISalArLedgerService _arLedger;

    private const short StDraft = 1, StPosted = 2, StCancelled = 3;

    public Sal1102Service(ISalDbContext db, ICompanyBranchContext ctx, ISalArLedgerService arLedger)
    {
        _db = db;
        _ctx = ctx;
        _arLedger = arLedger;
    }

    public async Task<List<Sal1102ReceiptDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var custs = await _db.SalCustomers.AsNoTracking().Where(c => c.IsDeleted == 0).ToDictionaryAsync(c => c.CustomerNo, c => c.CustomerName, ct);

        var list = await _db.SalReceipts
            .AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.IsDeleted == 0)
            .OrderByDescending(r => r.ReceiptNo)
            .ToListAsync(ct);

        return list.Select(r => ToDto(r, custs.ContainsKey(r.CustomerNo) ? custs[r.CustomerNo] : null, null)).ToList();
    }

    public async Task<Sal1102ReceiptDto> GetDetailAsync(long receiptNo, CancellationToken ct = default)
    {
        var r = await _db.SalReceipts.AsNoTracking().FirstOrDefaultAsync(x => x.ReceiptNo == receiptNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Receipt not found: {receiptNo}");

        var custName = await _db.SalCustomers.Where(c => c.CustomerNo == r.CustomerNo).Select(c => c.CustomerName).FirstOrDefaultAsync(ct);
        var allocs = await GetAllocationsAsync(receiptNo, ct);

        return ToDto(r, custName, allocs);
    }

    public async Task<List<Sal1102OpenInvoiceDto>> GetOpenInvoicesAsync(long customerNo, CancellationToken ct = default)
    {
        return await _db.SalInvoices
            .AsNoTracking()
            .Where(i => i.CustomerNo == customerNo && i.Status == 2 && i.DueAmount > 0 && i.IsDeleted == 0)
            .OrderBy(i => i.InvoiceDate)
            .Select(i => new Sal1102OpenInvoiceDto
            {
                InvoiceNo = i.InvoiceNo,
                InvoiceId = i.InvoiceId,
                InvoiceDate = i.InvoiceDate,
                GrandTotal = i.GrandTotal,
                PaidAmount = i.PaidAmount,
                DueAmount = i.DueAmount
            })
            .ToListAsync(ct);
    }

    public async Task<Sal1102ReceiptDto> SaveAsync(Sal1102ReceiptDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.CustomerNo <= 0) throw new ValidationException("Customer is required");
        if (dto.Amount <= 0) throw new ValidationException("Receipt amount must be greater than zero");

        decimal allocatedAmount = dto.Allocations != null ? dto.Allocations.Sum(a => a.AllocatedAmount) : 0m;
        if (allocatedAmount > dto.Amount) throw new ValidationException("Allocated amount cannot exceed total receipt amount");

        SalReceipt r;
        if (dto.ReceiptNo.HasValue && dto.ReceiptNo.Value > 0)
        {
            r = await _db.SalReceipts.FirstOrDefaultAsync(x => x.ReceiptNo == dto.ReceiptNo.Value && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Receipt not found: {dto.ReceiptNo}");
            if (r.Status != StDraft) throw new ValidationException("Only a Draft receipt can be edited");
            r.Amount = dto.Amount;
            r.AllocatedAmount = allocatedAmount;
            r.UnallocatedAmount = dto.Amount - allocatedAmount;
            r.PaymentMethod = dto.PaymentMethod;
            r.GlAccountNo = dto.GlAccountNo;
            r.ChequeNo = dto.ChequeNo;
            r.ChequeDate = dto.ChequeDate;
            r.TxnRef = dto.TxnRef;
            r.Remarks = dto.Remarks;
            r.UpdatedBy = _ctx.CurrentUserNo(); r.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            r = new SalReceipt
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                ReceiptId = $"RCT-{DateTime.UtcNow:yyyyMMddHHmmss}",
                ReceiptDate = dto.ReceiptDate != default ? dto.ReceiptDate : DateTime.UtcNow.Date,
                CustomerNo = dto.CustomerNo,
                PaymentMethod = dto.PaymentMethod,
                Amount = dto.Amount,
                AllocatedAmount = allocatedAmount,
                UnallocatedAmount = dto.Amount - allocatedAmount,
                GlAccountNo = dto.GlAccountNo,
                ChequeNo = dto.ChequeNo,
                ChequeDate = dto.ChequeDate,
                TxnRef = dto.TxnRef,
                Status = StDraft,
                Remarks = dto.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.SalReceipts.Add(r);
            await _db.SaveChangesAsync(ct);
            r.ReceiptId = $"RCT-{r.ReceiptNo:D6}";
        }

        if (dto.Allocations != null && dto.Allocations.Count > 0)
        {
            await ReplaceAllocationsAsync(r.ReceiptNo, dto.Allocations, ct);
        }

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(r.ReceiptNo, ct);
    }

    public async Task<Sal1102ReceiptDto> PostAsync(long receiptNo, CancellationToken ct = default)
    {
        var r = await _db.SalReceipts.FirstOrDefaultAsync(x => x.ReceiptNo == receiptNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Receipt not found: {receiptNo}");

        if (r.Status == StPosted) return await GetDetailAsync(receiptNo, ct);
        if (r.Status != StDraft) throw new ValidationException("Only a Draft receipt can be posted");

        await _arLedger.CreditAsync(r.CustomerNo, r.Amount, "RECEIPT", r.ReceiptNo, r.ReceiptId, $"Customer Receipt {r.ReceiptId}", ct);

        var allocs = await _db.SalReceiptAllocs.Where(a => a.ReceiptNo == receiptNo && a.IsDeleted == 0).ToListAsync(ct);
        foreach (var a in allocs)
        {
            var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == a.InvoiceNo && i.IsDeleted == 0, ct);
            if (inv != null)
            {
                inv.PaidAmount += a.AllocatedAmount;
                inv.DueAmount -= a.AllocatedAmount;
                inv.PaymentStatus = inv.DueAmount <= 0 ? (short)3 : (short)2;
                inv.UpdatedBy = _ctx.CurrentUserNo(); inv.UpdatedAt = DateTime.UtcNow;
            }
        }

        var payload = new GlPostingPayload
        {
            VoucherDate = r.ReceiptDate,
            Narration = $"Customer Receipt {r.ReceiptId}",
            BranchNo = r.BranchNo,
            Legs = new List<GlPostingPayload.Leg>
            {
                new() { LegKey = r.PaymentMethod == 1 ? "CASH" : "BANK", Amount = r.Amount, DrCr = "dr" },
                new() { LegKey = "RECEIVABLE", Amount = r.Amount, DrCr = "cr", PartyType = 1, PartyNo = r.CustomerNo }
            }
        };

        var ev = new EventOutbox
        {
            CompanyNo = r.CompanyNo,
            BranchNo = r.BranchNo,
            EventType = "CustomerReceiptPosted",
            AggregateType = "SalReceipt",
            AggregateId = r.ReceiptNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1, CreatedAt = DateTime.UtcNow
        };
        _db.EventOutboxes.Add(ev);

        r.Status = StPosted;
        r.PostedBy = _ctx.CurrentUserNo();
        r.PostedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(receiptNo, ct);
    }

    public async Task DeleteAsync(long receiptNo, CancellationToken ct = default)
    {
        var r = await _db.SalReceipts.FirstOrDefaultAsync(x => x.ReceiptNo == receiptNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Receipt not found: {receiptNo}");

        if (r.Status != StDraft) throw new ValidationException("Only a Draft receipt can be deleted");

        r.IsDeleted = 1; r.IsActive = 0;
        r.DeletedBy = _ctx.CurrentUserNo(); r.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ReplaceAllocationsAsync(long receiptNo, List<Sal1102AllocDto> allocs, CancellationToken ct)
    {
        var existing = await _db.SalReceiptAllocs.Where(a => a.ReceiptNo == receiptNo && a.IsDeleted == 0).ToListAsync(ct);
        foreach (var e in existing) { e.IsDeleted = 1; e.DeletedBy = _ctx.CurrentUserNo(); e.DeletedAt = DateTime.UtcNow; }

        foreach (var a in allocs)
        {
            if (a.AllocatedAmount <= 0) continue;
            var entity = new SalReceiptAlloc
            {
                ReceiptNo = receiptNo,
                InvoiceNo = a.InvoiceNo,
                AllocatedAmount = a.AllocatedAmount,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.SalReceiptAllocs.Add(entity);
        }
    }

    private async Task<List<Sal1102AllocDto>> GetAllocationsAsync(long receiptNo, CancellationToken ct)
    {
        var invIds = await _db.SalInvoices.AsNoTracking().Where(i => i.IsDeleted == 0).ToDictionaryAsync(i => i.InvoiceNo, i => i.InvoiceId, ct);

        return await _db.SalReceiptAllocs
            .AsNoTracking()
            .Where(a => a.ReceiptNo == receiptNo && a.IsDeleted == 0)
            .Select(a => new Sal1102AllocDto
            {
                AllocNo = a.AllocNo,
                ReceiptNo = a.ReceiptNo,
                InvoiceNo = a.InvoiceNo,
                InvoiceId = invIds.ContainsKey(a.InvoiceNo) ? invIds[a.InvoiceNo] : null,
                AllocatedAmount = a.AllocatedAmount
            })
            .ToListAsync(ct);
    }

    private static Sal1102ReceiptDto ToDto(SalReceipt r, string? custName, List<Sal1102AllocDto>? allocs) => new()
    {
        ReceiptNo = r.ReceiptNo,
        ReceiptId = r.ReceiptId,
        ReceiptDate = r.ReceiptDate,
        CustomerNo = r.CustomerNo,
        CustomerName = custName,
        PaymentMethod = r.PaymentMethod,
        Amount = r.Amount,
        AllocatedAmount = r.AllocatedAmount,
        UnallocatedAmount = r.UnallocatedAmount,
        GlAccountNo = r.GlAccountNo,
        ChequeNo = r.ChequeNo,
        ChequeDate = r.ChequeDate,
        TxnRef = r.TxnRef,
        Status = r.Status,
        Remarks = r.Remarks,
        IsActive = r.IsActive,
        RowVersion = r.RowVersion,
        Allocations = allocs ?? new()
    };
}
