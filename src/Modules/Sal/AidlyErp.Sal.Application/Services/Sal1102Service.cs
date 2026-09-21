using AidlyErp.Shared.Core;
using AidlyErp.Sal.Application.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Contracts;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Domain;

namespace AidlyErp.Sal.Application.Services;

public interface ISal1102Service
{
    Task<List<Sal1102ReceiptDto>> GetListAsync(CancellationToken ct = default);
    Task<Sal1102ReceiptDto> GetDetailAsync(long receiptNo, CancellationToken ct = default);
    Task<List<Sal1102OpenInvoiceDto>> GetOpenInvoicesAsync(long customerNo, CancellationToken ct = default);
    Task<Sal1102ReceiptDto> SaveAsync(Sal1102ReceiptDto dto, CancellationToken ct = default);
    Task<Sal1102ReceiptDto> PostAsync(long receiptNo, CancellationToken ct = default);
    Task<Sal1102ReceiptDto> CancelAsync(long receiptNo, string? reason, CancellationToken ct = default);
    Task DeleteAsync(long receiptNo, CancellationToken ct = default);
}

public class Sal1102Service : ISal1102Service
{
    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly ISalArLedgerService _arLedger;

    private const short StDraft = 1, StPosted = 2, StCancelled = 3;

    /// <summary>AR ledger ref_doc_type for a receipt adjustment / reversal (Java AR_REF_ADJ).</summary>
    private const short ArRefAdjustment = 5;

    private readonly IDocSequenceGenerator _docSeq;
    private const string DocSeqReceipt = "SAL_RECEIPT";

    public Sal1102Service(ISalDbContext db, ICompanyBranchContext ctx, ISalArLedgerService arLedger,
                          IDocSequenceGenerator docSeq)
    {
        _docSeq = docSeq;
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
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var r = await _db.SalReceipts.AsNoTracking().FirstOrDefaultAsync(x => x.ReceiptNo == receiptNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
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
            r = await _db.SalReceipts.FirstOrDefaultAsync(x => x.ReceiptNo == dto.ReceiptNo.Value && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
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
            // Number from the ID generator (SYS_1301), per company/branch/year.
            string receiptId = await _docSeq.NextAsync(companyNo, branchNo, DocSeqReceipt, "RCT", 6, ct);

            r = new SalReceipt
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                ReceiptId = receiptId,
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
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var r = await _db.SalReceipts.FirstOrDefaultAsync(x => x.ReceiptNo == receiptNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Receipt not found: {receiptNo}");

        if (r.Status == StPosted) return await GetDetailAsync(receiptNo, ct);
        if (r.Status != StDraft) throw new ValidationException("Only a Draft receipt can be posted");

        await _arLedger.CreditAsync(r.CustomerNo, r.Amount, 3, r.ReceiptNo.ToString(), r.ReceiptNo, $"Customer Receipt {r.ReceiptId}", ct);

        var allocs = await _db.SalReceiptAllocs.Where(a => a.ReceiptNo == receiptNo && a.IsDeleted == 0).ToListAsync(ct);

        // One query for every allocated invoice rather than one per allocation row.
        var invoiceNos = allocs.Where(a => a.InvoiceNo.HasValue).Select(a => a.InvoiceNo!.Value).Distinct().ToList();
        var invoices = await _db.SalInvoices
            .Where(i => invoiceNos.Contains(i.InvoiceNo) && i.CompanyNo == companyNo && i.IsDeleted == 0)
            .ToDictionaryAsync(i => i.InvoiceNo, ct);

        foreach (var a in allocs)
        {
            if (a.InvoiceNo is null || !invoices.TryGetValue(a.InvoiceNo.Value, out var inv)) continue;

            // Paying more than an invoice owes would drive its due negative.
            if (a.AllocatedAmount > inv.DueAmount)
                throw new ValidationException($"Allocation exceeds due on invoice {inv.InvoiceId}");

            inv.PaidAmount += a.AllocatedAmount;
            inv.DueAmount -= a.AllocatedAmount;
            inv.PaymentStatus = inv.DueAmount <= 0 ? (short)3 : (short)2;
            inv.UpdatedBy = _ctx.CurrentUserNo(); inv.UpdatedAt = DateTime.UtcNow;
        }

        var payload = new GlPostingPayload
        {
            VoucherDate = r.ReceiptDate,
            Narration = $"Customer Receipt {r.ReceiptId}",
            BranchNo = r.BranchNo,
            Legs = new List<GlPostingPayload.Leg>
            {
                new() { LegKey = "CASH", Amount = r.Amount, DrCr = "dr" },
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
        r.PostedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(receiptNo, ct);
    }

    /// <summary>
    /// Port of Java Sal1102Service.cancel. Unwinds a posted receipt: restores the
    /// allocated amounts on every invoice it paid, debits the AR sub-ledger back, and
    /// emits a reversing GL event.
    ///
    /// Uses a distinct event_type — CustomerReceiptReversed — so the posting engine's
    /// idempotency guard on (source_doc_type, source_doc_no) does not dedupe it against
    /// the original CustomerReceiptPosted.
    /// </summary>
    public async Task<Sal1102ReceiptDto> CancelAsync(long receiptNo, string? reason, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        var r = await _db.SalReceipts.FirstOrDefaultAsync(x => x.ReceiptNo == receiptNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Receipt not found: {receiptNo}");

        if (r.Status == StCancelled) throw new ValidationException("Receipt is already cancelled");
        if (r.Status != StPosted) throw new ValidationException("Only a Posted receipt can be cancelled");

        // Put the allocated amounts back on each invoice, mirroring PostAsync exactly.
        var allocs = await _db.SalReceiptAllocs.Where(a => a.ReceiptNo == receiptNo && a.IsDeleted == 0).ToListAsync(ct);
        var invoiceNos = allocs.Where(a => a.InvoiceNo.HasValue).Select(a => a.InvoiceNo!.Value).Distinct().ToList();
        var invoices = await _db.SalInvoices
            .Where(i => invoiceNos.Contains(i.InvoiceNo) && i.IsDeleted == 0)
            .ToDictionaryAsync(i => i.InvoiceNo, ct);

        foreach (var a in allocs)
        {
            if (!a.InvoiceNo.HasValue || !invoices.TryGetValue(a.InvoiceNo.Value, out var inv)) continue;
            inv.PaidAmount -= a.AllocatedAmount;
            inv.DueAmount += a.AllocatedAmount;
            // 1 = unpaid, 2 = partly paid, 3 = fully paid
            inv.PaymentStatus = inv.PaidAmount <= 0 ? (short)1 : inv.DueAmount <= 0 ? (short)3 : (short)2;
            inv.UpdatedBy = _ctx.CurrentUserNo(); inv.UpdatedAt = DateTime.UtcNow;
        }

        // PostAsync credited AR by the receipt amount; cancelling debits it back.
        await _arLedger.DebitAsync(r.CustomerNo, r.Amount, ArRefAdjustment,
            r.ReceiptId, r.ReceiptNo, $"Receipt cancelled {r.ReceiptId}", ct);

        var payload = new GlPostingPayload
        {
            VoucherDate = r.ReceiptDate,
            Narration = $"Customer receipt reversal {r.ReceiptId}",
            BranchNo = r.BranchNo,
            Legs = new List<GlPostingPayload.Leg>
            {
                // Original was Dr CASH / Cr RECEIVABLE — reversal swaps both.
                new() { LegKey = "CASH", Amount = r.Amount, DrCr = "cr" },
                new() { LegKey = "RECEIVABLE", Amount = r.Amount, DrCr = "dr", PartyType = 1, PartyNo = r.CustomerNo }
            }
        };

        _db.EventOutboxes.Add(new EventOutbox
        {
            CompanyNo = r.CompanyNo,
            BranchNo = r.BranchNo,
            EventType = "CustomerReceiptReversed",
            AggregateType = "SalReceipt",
            AggregateId = r.ReceiptNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        });

        r.Status = StCancelled;
        if (!string.IsNullOrWhiteSpace(reason))
            r.Remarks = string.IsNullOrWhiteSpace(r.Remarks) ? $"Cancelled: {reason}" : $"{r.Remarks} | Cancelled: {reason}";
        r.UpdatedBy = _ctx.CurrentUserNo(); r.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(receiptNo, ct);
    }

    public async Task DeleteAsync(long receiptNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        var r = await _db.SalReceipts.FirstOrDefaultAsync(x => x.ReceiptNo == receiptNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
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
        var allocs = await _db.SalReceiptAllocs
            .AsNoTracking()
            .Where(a => a.ReceiptNo == receiptNo && a.IsDeleted == 0)
            .Select(a => new Sal1102AllocDto
            {
                ReceiptAllocNo = a.ReceiptAllocNo,
                ReceiptNo = a.ReceiptNo,
                InvoiceNo = a.InvoiceNo ?? 0,
                AllocatedAmount = a.AllocatedAmount
            })
            .ToListAsync(ct);

        if (allocs.Count == 0) return allocs;

        // Only the invoices these allocations point at, and only within this company. The previous
        // version read every invoice row in the database — every tenant's — to label a handful of
        // rows, which is both a cross-company read and a table scan that grows forever.
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var invoiceNos = allocs.Select(a => a.InvoiceNo).Where(n => n > 0).Distinct().ToList();

        var invoices = await _db.SalInvoices.AsNoTracking()
            .Where(i => invoiceNos.Contains(i.InvoiceNo) && i.CompanyNo == companyNo && i.IsDeleted == 0)
            .Select(i => new { i.InvoiceNo, i.InvoiceId, i.DueAmount })
            .ToDictionaryAsync(i => i.InvoiceNo, ct);

        foreach (var a in allocs)
        {
            if (!invoices.TryGetValue(a.InvoiceNo, out var inv)) continue;
            a.InvoiceId = inv.InvoiceId;
            // Outstanding as it stands now. On a saved receipt the allocation has already been
            // applied, so this is what is left after it — which is the number the user needs when
            // deciding whether to allocate more.
            a.InvoiceDue = inv.DueAmount;
        }

        return allocs;
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
        IsActive = r.IsActive ?? 0,
        RowVersion = r.RowVersion,
        Allocations = allocs ?? new()
    };
}
