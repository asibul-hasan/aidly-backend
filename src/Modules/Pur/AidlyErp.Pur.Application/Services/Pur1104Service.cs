using System.Text.Json;
using AidlyErp.Fin.Contracts;
using AidlyErp.Pur.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Domain;

namespace AidlyErp.Pur.Application.Services;

public interface IPur1104Service
{
    Task<List<Pur1104PaymentDto>> GetListAsync(CancellationToken ct = default);
    Task<List<Pur1104OpenInvoiceDto>> GetOpenInvoicesAsync(long supplierNo, CancellationToken ct = default);
    Task<Pur1104PaymentDto> GetDetailAsync(long paymentNo, CancellationToken ct = default);
    Task<Pur1104PaymentDto> CreateAsync(Pur1104PaymentDto dto, CancellationToken ct = default);
    Task<Pur1104PaymentDto> CancelAsync(long paymentNo, string? reason, CancellationToken ct = default);
    // Legacy surface kept for controller compat
    Task<Pur1104PaymentDto> SaveAsync(Pur1104PaymentDto dto, CancellationToken ct = default);
    Task<Pur1104PaymentDto> PostAsync(long paymentNo, CancellationToken ct = default);
}

/// <summary>
/// PUR_1104 Supplier Payment — full port of Java Pur1104Service.
/// Posts immediately on create: debits the AP sub-ledger, allocates to invoices,
/// emits SupplierPaymentPosted (Dr AP / Cr Cash-Bank) to the GL outbox.
/// Cancel fully reverses. Period guard and gap-free document numbering are wired.
/// </summary>
public class Pur1104Service : IPur1104Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IPurApLedgerService _apLedger;
    private readonly IUnitOfWork<IPurDbContext> _uow;
    private readonly IDocSequenceGenerator _docSeq;
    private readonly IFinCalendar _calendar;

    private const short StPosted = 2, StCancelled = 3;
    private const short InvPosted = 2, InvPartRet = 3;
    private const short ApRefPayment = 3, ApRefAdj = 5;
    private static readonly HashSet<short> Methods = [1, 2, 3, 4, 5, 6];
    private const string DocSeq = "PUR_PAY";
    private const decimal Cent = 0.0001m;

    public Pur1104Service(IPurDbContext db, ICompanyBranchContext ctx, IPurApLedgerService apLedger,
                          IUnitOfWork<IPurDbContext> uow, IDocSequenceGenerator docSeq, IFinCalendar calendar)
    {
        _db = db;
        _ctx = ctx;
        _apLedger = apLedger;
        _uow = uow;
        _docSeq = docSeq;
        _calendar = calendar;
    }

    // ── reads ──────────────────────────────────────────────────────────────────

    public async Task<List<Pur1104PaymentDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        long branchNo = _ctx.CurrentBranchNo() ?? 0;

        var sups = await _db.PurSuppliers.AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == 0)
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        var list = await _db.PurPayments
            .AsNoTracking()
            .Where(p => p.CompanyNo == companyNo && p.BranchNo == branchNo && p.IsDeleted == 0)
            .OrderByDescending(p => p.PaymentNo)
            .ToListAsync(ct);

        return list.Select(p => ToHeaderDto(p, sups)).ToList();
    }

    public async Task<List<Pur1104OpenInvoiceDto>> GetOpenInvoicesAsync(long supplierNo, CancellationToken ct = default)
    {
        if (supplierNo <= 0) return new();
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        long branchNo = _ctx.CurrentBranchNo() ?? 0;

        return await _db.PurInvoices.AsNoTracking()
            .Where(i => i.CompanyNo == companyNo && i.BranchNo == branchNo
                        && i.SupplierNo == supplierNo && i.IsDeleted == 0
                        && (i.Status == InvPosted || i.Status == InvPartRet)
                        && i.DueAmount > 0)
            // OLDEST first. PUR_1104's Auto-allocate walks this list in order and the form states
            // it "applies it to the oldest dues first"; ordering newest-first made it settle the
            // newest invoice and leave the oldest debt standing — the opposite of FIFO settlement.
            .OrderBy(i => i.InvoiceDate).ThenBy(i => i.InvoiceNo)
            .Select(i => new Pur1104OpenInvoiceDto
            {
                InvoiceNo = i.InvoiceNo,
                InvoiceId = i.InvoiceId,
                SupplierInvoiceNo = i.SupplierInvoiceNo,
                InvoiceDate = i.InvoiceDate,
                GrandTotal = i.GrandTotal,
                DueAmount = i.DueAmount
            })
            .ToListAsync(ct);
    }

    public async Task<Pur1104PaymentDto> GetDetailAsync(long paymentNo, CancellationToken ct = default)
    {
        var p = await RequireAsync(paymentNo, ct);

        var sups = await _db.PurSuppliers.AsNoTracking()
            .Where(s => s.CompanyNo == p.CompanyNo && s.IsDeleted == 0)
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        var dto = ToHeaderDto(p, sups);

        var allocs = await _db.PurPaymentAllocs.AsNoTracking()
            .Where(a => a.PaymentNo == paymentNo && a.IsDeleted == 0)
            .OrderBy(a => a.PaymentAllocNo)
            .ToListAsync(ct);

        var invNos = allocs.Where(a => a.InvoiceNo.HasValue).Select(a => a.InvoiceNo!.Value).Distinct().ToList();
        var invoices = await _db.PurInvoices.AsNoTracking()
            .Where(i => invNos.Contains(i.InvoiceNo) && i.IsDeleted == 0)
            .ToDictionaryAsync(i => i.InvoiceNo, ct);

        dto.Allocations = allocs.Select(a =>
        {
            var d = new Pur1104AllocDto
            {
                PaymentAllocNo = a.PaymentAllocNo,
                PaymentNo = a.PaymentNo,
                InvoiceNo = a.InvoiceNo ?? 0,
                AllocatedAmount = a.AllocatedAmount
            };
            if (a.InvoiceNo.HasValue && invoices.TryGetValue(a.InvoiceNo.Value, out var inv))
            {
                d.InvoiceId = inv.InvoiceId;
                d.InvoiceDate = inv.InvoiceDate;
                d.InvoiceDue = inv.DueAmount;
            }
            return d;
        }).ToList();

        return dto;
    }

    // ── post on create ─────────────────────────────────────────────────────────

    public async Task<Pur1104PaymentDto> CreateAsync(Pur1104PaymentDto dto, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token => await CreateInternalAsync(dto, token), ct);
    }

    private async Task<Pur1104PaymentDto> CreateInternalAsync(Pur1104PaymentDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context required");

        // ── validation ──
        if (dto.SupplierNo <= 0) throw new ValidationException("Supplier is required");
        if (dto.PaymentMethod <= 0 || !Methods.Contains(dto.PaymentMethod))
            throw new ValidationException("Invalid payment method");
        decimal amount = dto.Amount;
        if (amount <= 0) throw new ValidationException("Payment amount must be positive");

        var supplier = await _db.PurSuppliers.FirstOrDefaultAsync(s => s.SupplierNo == dto.SupplierNo && s.IsDeleted == 0, ct)
            ?? throw new NotFoundException("Supplier not found");
        if (supplier.CompanyNo != companyNo) throw new ValidationException("Supplier not in your company");

        // ── period guard ──
        var payDate = dto.PaymentDate != default ? dto.PaymentDate : DateTime.UtcNow.Date;
        var payDay = DateOnly.FromDateTime(payDate);
        var finYear = await _calendar.FindYearForDateAsync(companyNo, payDay, ct)
            ?? throw new ValidationException($"No financial year configured for date {payDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, payDay, ct)
            ?? throw new ValidationException($"No financial period configured for date {payDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot post: period '{finPeriod.FinPeriodName}' is not Open");

        // ── validate + collect allocations ──
        var allocs = dto.Allocations ?? new();
        decimal allocated = 0m;
        var touched = new List<(PurInvoice inv, decimal amt)>();

        foreach (var a in allocs)
        {
            if (a.InvoiceNo <= 0 || a.AllocatedAmount <= 0) continue;
            var inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == a.InvoiceNo && i.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Invoice not found: {a.InvoiceNo}");
            if (inv.CompanyNo != companyNo || inv.SupplierNo != dto.SupplierNo)
                throw new ValidationException($"Invoice {inv.InvoiceId} is not this supplier's");
            if (a.AllocatedAmount > inv.DueAmount)
                throw new ValidationException($"Allocation exceeds due on invoice {inv.InvoiceId}");
            allocated += a.AllocatedAmount;
            touched.Add((inv, a.AllocatedAmount));
        }
        if (allocated - amount > Cent)
            throw new ValidationException("Allocations exceed the payment amount");

        // ── document numbering ──
        string paymentId = await _docSeq.NextAsync(companyNo, branchNo, DocSeq, "PAY", 6, ct);

        var pay = new PurPayment
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            PaymentId = paymentId,
            PaymentDate = payDate,
            SupplierNo = dto.SupplierNo,
            PaymentMethod = dto.PaymentMethod,
            Amount = amount,
            AllocatedAmount = allocated,
            UnallocatedAmount = amount - allocated,
            // NOT NULL in pur_payment. The period guard above already resolved both; not carrying
            // them onto the entity meant no supplier payment could be saved. Fifth occurrence of
            // this same defect across the modules.
            FinYearNo = finYear.FinYearNo,
            FinPeriodNo = finPeriod.FinPeriodNo,
            Remarks = dto.Remarks,
            Status = StPosted,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.PurPayments.Add(pay);
        await _db.SaveChangesAsync(ct);

        // ── allocation rows + apply to invoices ──
        foreach (var (inv, amt) in touched)
        {
            var alloc = new PurPaymentAlloc
            {
                PaymentNo = pay.PaymentNo,
                InvoiceNo = inv.InvoiceNo,
                AllocatedAmount = amt,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurPaymentAllocs.Add(alloc);

            inv.PaidAmount += amt;
            inv.DueAmount -= amt;
            inv.PaymentStatus = InvoicePaymentStatus(inv);
        }
        await _db.SaveChangesAsync(ct);

        // ── AP sub-ledger ──
        await _apLedger.DebitAsync(pay.SupplierNo, amount, ApRefPayment, pay.PaymentId, pay.PaymentNo,
            $"Supplier payment {pay.PaymentId}", finYear.FinYearNo, finPeriod.FinPeriodNo, payDate, ct);

        // ── GL emit ──
        await EmitGlAsync(pay, supplier, "SupplierPaymentPosted", false, ct);

        return await GetDetailAsync(pay.PaymentNo, ct);
    }

    // ── cancel ─────────────────────────────────────────────────────────────────

    public async Task<Pur1104PaymentDto> CancelAsync(long paymentNo, string? reason, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token => await CancelInternalAsync(paymentNo, reason, token), ct);
    }

    private async Task<Pur1104PaymentDto> CancelInternalAsync(long paymentNo, string? reason, CancellationToken ct)
    {
        var pay = await RequireAsync(paymentNo, ct);
        if (pay.Status != StPosted) throw new ValidationException("Only a Posted payment can be cancelled");

        var supplier = await _db.PurSuppliers.FirstOrDefaultAsync(s => s.SupplierNo == pay.SupplierNo && s.IsDeleted == 0, ct)
            ?? throw new NotFoundException("Supplier not found");

        // ── period guard ──
        var payDay = DateOnly.FromDateTime(pay.PaymentDate);
        var finYear = await _calendar.FindYearForDateAsync(pay.CompanyNo, payDay, ct)
            ?? throw new ValidationException($"No financial year configured for date {pay.PaymentDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, payDay, ct)
            ?? throw new ValidationException($"No financial period configured for date {pay.PaymentDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot cancel: period '{finPeriod.FinPeriodName}' is not Open");

        // ── restore invoice dues ──
        var allocs = await _db.PurPaymentAllocs
            .Where(a => a.PaymentNo == paymentNo && a.IsDeleted == 0)
            .ToListAsync(ct);

        foreach (var a in allocs)
        {
            if (!a.InvoiceNo.HasValue) continue;
            var inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == a.InvoiceNo.Value && i.IsDeleted == 0, ct);
            if (inv != null)
            {
                inv.PaidAmount -= a.AllocatedAmount;
                inv.DueAmount += a.AllocatedAmount;
                inv.PaymentStatus = InvoicePaymentStatus(inv);
            }
        }

        // ── reverse AP ──
        await _apLedger.CreditAsync(pay.SupplierNo, pay.Amount, ApRefAdj, pay.PaymentId, pay.PaymentNo,
            $"Payment cancelled {pay.PaymentId}", finYear.FinYearNo, finPeriod.FinPeriodNo, pay.PaymentDate, ct);

        // ── reversing GL ──
        await EmitGlAsync(pay, supplier, "SupplierPaymentReversed", true, ct);

        pay.Status = StCancelled;
        if (reason != null)
            pay.Remarks = (pay.Remarks != null ? pay.Remarks + " | " : "") + "Cancelled: " + reason;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(paymentNo, ct);
    }

    // ── legacy surface (controller compat) ─────────────────────────────────────

    public Task<Pur1104PaymentDto> SaveAsync(Pur1104PaymentDto dto, CancellationToken ct = default)
        => CreateAsync(dto, ct);

    public Task<Pur1104PaymentDto> PostAsync(long paymentNo, CancellationToken ct = default)
        => Task.FromResult(new Pur1104PaymentDto { PaymentNo = paymentNo, Status = StPosted });

    // ── GL emit ────────────────────────────────────────────────────────────────

    private async Task EmitGlAsync(PurPayment pay, PurSupplier supplier, string eventType, bool reverse, CancellationToken ct)
    {
        decimal amount = pay.Amount;
        if (amount <= 0) return;

        var legs = new List<GlPostingPayload.Leg>
        {
            new() { LegKey = "PAYABLE", Amount = amount, DrCr = reverse ? "cr" : "dr", PartyType = 2, PartyNo = supplier.SupplierNo },
            new() { LegKey = "CASH", Amount = amount, DrCr = reverse ? "dr" : "cr" }
        };

        var payload = new GlPostingPayload
        {
            VoucherDate = pay.PaymentDate,
            Narration = (reverse ? "Supplier payment reversal " : "Supplier payment ") + pay.PaymentId,
            BranchNo = pay.BranchNo,
            Legs = legs
        };

        var ev = new EventOutbox
        {
            CompanyNo = pay.CompanyNo,
            BranchNo = pay.BranchNo,
            EventType = eventType,
            AggregateType = "PUR_PAYMENT",
            AggregateId = pay.PaymentNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.EventOutboxes.Add(ev);
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static short InvoicePaymentStatus(PurInvoice inv)
    {
        if (inv.PaidAmount <= 0) return 1;
        return inv.PaidAmount >= inv.GrandTotal ? (short)3 : (short)2;
    }

    private static Pur1104PaymentDto ToHeaderDto(PurPayment p, Dictionary<long, string> sups) => new()
    {
        PaymentNo = p.PaymentNo,
        PaymentId = p.PaymentId,
        PaymentDate = p.PaymentDate,
        SupplierNo = p.SupplierNo,
        SupplierName = sups.GetValueOrDefault(p.SupplierNo),
        PaymentMethod = p.PaymentMethod,
        Amount = p.Amount,
        AllocatedAmount = p.AllocatedAmount,
        UnallocatedAmount = p.UnallocatedAmount,
        Status = p.Status,
        Remarks = p.Remarks,
        IsActive = p.IsActive ?? 0,
        RowVersion = p.RowVersion
    };

    private async Task<PurPayment> RequireAsync(long paymentNo, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        var p = await _db.PurPayments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PaymentNo == paymentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Payment not found: {paymentNo}");
        if (p.CompanyNo != companyNo) throw new ValidationException("Payment belongs to another company");
        return p;
    }
}
