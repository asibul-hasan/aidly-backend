using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Fin.Contract;
using AidlyErp.Application.Sal.Dto;
using AidlyErp.Domain.Sal;
using AidlyErp.Domain.Sys;

namespace AidlyErp.Application.Sal.Services;

public interface ISal1001Service
{
    Task<List<Sal1001InvoiceDto>> GetListAsync(CancellationToken ct = default);
    Task<Sal1001InvoiceDto> GetDetailAsync(long invoiceNo, CancellationToken ct = default);
    Task<Sal1001InvoiceDto> SaveAsync(Sal1001InvoiceDto dto, CancellationToken ct = default);
    Task<Sal1001InvoiceDto> SubmitAsync(long invoiceNo, CancellationToken ct = default);
    Task<Sal1001InvoiceDto> PostAsync(long invoiceNo, CancellationToken ct = default);
    Task<Sal1001InvoiceDto> CancelAsync(long invoiceNo, string? reason, CancellationToken ct = default);
    Task DeleteAsync(long invoiceNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long invoiceNo, bool approved, CancellationToken ct = default);
}

public class Sal1001Service : ISal1001Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly ISalArLedgerService _arLedger;
    private readonly IApprovalService _approvalService;

    public const string DocType = "SAL_INVOICE";
    private const short StDraft = 1, StPosted = 2, StCancelled = 3;

    public Sal1001Service(IApplicationDbContext db, ICompanyBranchContext ctx, ISalArLedgerService arLedger, IApprovalService approvalService)
    {
        _db = db;
        _ctx = ctx;
        _arLedger = arLedger;
        _approvalService = approvalService;
    }

    public async Task<List<Sal1001InvoiceDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var custs = await _db.SalCustomers.AsNoTracking().Where(c => c.IsDeleted == 0).ToDictionaryAsync(c => c.CustomerNo, c => c.CustomerName, ct);

        var list = await _db.SalInvoices
            .AsNoTracking()
            .Where(i => i.CompanyNo == companyNo && i.IsDeleted == 0)
            .OrderByDescending(i => i.InvoiceNo)
            .ToListAsync(ct);

        return list.Select(i => ToDto(i, custs.ContainsKey(i.CustomerNo) ? custs[i.CustomerNo] : null, null)).ToList();
    }

    public async Task<Sal1001InvoiceDto> GetDetailAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await _db.SalInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        var custName = await _db.SalCustomers.Where(c => c.CustomerNo == inv.CustomerNo).Select(c => c.CustomerName).FirstOrDefaultAsync(ct);
        var lines = await GetLinesAsync(invoiceNo, ct);

        return ToDto(inv, custName, lines);
    }

    public async Task<Sal1001InvoiceDto> SaveAsync(Sal1001InvoiceDto dto, CancellationToken ct = default)
    {
        return dto.InvoiceNo.HasValue && dto.InvoiceNo.Value > 0
            ? await UpdateAsync(dto.InvoiceNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    private async Task<Sal1001InvoiceDto> InsertAsync(Sal1001InvoiceDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = dto.BranchNo ?? _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.CustomerNo <= 0) throw new ValidationException("Customer is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Invoice lines are required");

        decimal subTotal = dto.Lines.Sum(l => l.Quantity * l.UnitPrice);
        decimal taxAmount = dto.Lines.Sum(l => l.TaxAmount);
        decimal grandTotal = subTotal + taxAmount + dto.ShippingCharge + dto.RoundOff - dto.LineDiscountTotal;
        decimal dueAmount = grandTotal - dto.PaidAmount;

        var inv = new SalInvoice
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            InvoiceId = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
            InvoiceDate = dto.InvoiceDate != default ? dto.InvoiceDate : DateTime.UtcNow.Date,
            SaleType = dto.SaleType,
            CustomerNo = dto.CustomerNo,
            WarehouseNo = dto.WarehouseNo > 0 ? dto.WarehouseNo : 1,
            Status = StDraft,
            SubTotal = subTotal,
            LineDiscountTotal = dto.LineDiscountTotal,
            TaxableAmount = subTotal - dto.LineDiscountTotal,
            TaxAmount = taxAmount,
            ShippingCharge = dto.ShippingCharge,
            RoundOff = dto.RoundOff,
            GrandTotal = grandTotal,
            PaidAmount = dto.PaidAmount,
            DueAmount = dueAmount,
            PaymentStatus = dueAmount <= 0 ? (short)3 : (dto.PaidAmount > 0 ? (short)2 : (short)1),
            DueDate = dto.DueDate ?? DateTime.UtcNow.AddDays(30),
            Remarks = dto.Remarks,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };

        _db.SalInvoices.Add(inv);
        await _db.SaveChangesAsync(ct);

        inv.InvoiceId = $"INV-{inv.InvoiceNo:D6}";
        await ReplaceLinesAsync(inv.InvoiceNo, dto.Lines, ct);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(inv.InvoiceNo, ct);
    }

    private async Task<Sal1001InvoiceDto> UpdateAsync(long invoiceNo, Sal1001InvoiceDto dto, CancellationToken ct)
    {
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be edited");

        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            decimal subTotal = dto.Lines.Sum(l => l.Quantity * l.UnitPrice);
            decimal taxAmount = dto.Lines.Sum(l => l.TaxAmount);
            decimal grandTotal = subTotal + taxAmount + dto.ShippingCharge + dto.RoundOff - dto.LineDiscountTotal;

            inv.SubTotal = subTotal;
            inv.LineDiscountTotal = dto.LineDiscountTotal;
            inv.TaxableAmount = subTotal - dto.LineDiscountTotal;
            inv.TaxAmount = taxAmount;
            inv.ShippingCharge = dto.ShippingCharge;
            inv.RoundOff = dto.RoundOff;
            inv.GrandTotal = grandTotal;
            inv.DueAmount = grandTotal - inv.PaidAmount;
            inv.PaymentStatus = inv.DueAmount <= 0 ? (short)3 : (inv.PaidAmount > 0 ? (short)2 : (short)1);

            await ReplaceLinesAsync(invoiceNo, dto.Lines, ct);
        }

        if (dto.Remarks != null) inv.Remarks = dto.Remarks;
        inv.UpdatedBy = _ctx.CurrentUserNo(); inv.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(invoiceNo, ct);
    }

    public async Task<Sal1001InvoiceDto> SubmitAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be submitted");

        var outcome = await _approvalService.RaiseAsync(DocType, inv.InvoiceNo, inv.InvoiceId, inv.GrandTotal, ct);
        if (outcome.AutoApproved)
        {
            await PostInternalAsync(inv, ct);
        }
        else
        {
            inv.ApprovalRequestNo = outcome.ApprovalRequestNo;
            await _db.SaveChangesAsync(ct);
        }

        return await GetDetailAsync(invoiceNo, ct);
    }

    public async Task<Sal1001InvoiceDto> PostAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        await PostInternalAsync(inv, ct);
        return await GetDetailAsync(invoiceNo, ct);
    }

    private async Task PostInternalAsync(SalInvoice inv, CancellationToken ct)
    {
        if (inv.Status == StPosted) return;
        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be posted");

        if (inv.DueAmount > 0)
        {
            await _arLedger.DebitAsync(inv.CustomerNo, inv.DueAmount, "INVOICE", inv.InvoiceNo, inv.InvoiceId, $"Sales Invoice {inv.InvoiceId}", ct);
        }

        var payload = new GlPostingPayload
        {
            VoucherDate = inv.InvoiceDate,
            Narration = $"Sales Invoice {inv.InvoiceId}",
            BranchNo = inv.BranchNo,
            Legs = new List<GlPostingPayload.Leg>
            {
                new() { LegKey = "RECEIVABLE", Amount = inv.DueAmount, DrCr = "dr", PartyType = 1, PartyNo = inv.CustomerNo },
                new() { LegKey = "CASH", Amount = inv.PaidAmount, DrCr = "dr" },
                new() { LegKey = "REVENUE", Amount = inv.SubTotal - inv.LineDiscountTotal, DrCr = "cr" },
                new() { LegKey = "VAT_OUTPUT", Amount = inv.TaxAmount, DrCr = "cr" }
            }
        };

        var ev = new EventOutbox
        {
            CompanyNo = inv.CompanyNo,
            BranchNo = inv.BranchNo,
            EventType = "SalesInvoicePosted",
            AggregateType = "SalInvoice",
            AggregateId = inv.InvoiceNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.EventOutboxes.Add(ev);

        inv.Status = StPosted;
        inv.PostedBy = _ctx.CurrentUserNo();
        inv.PostedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task ApplyApprovalOutcomeAsync(long invoiceNo, bool approved, CancellationToken ct = default)
    {
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (approved) await PostInternalAsync(inv, ct);
        else { inv.ApprovalRequestNo = null; await _db.SaveChangesAsync(ct); }
    }

    public async Task<Sal1001InvoiceDto> CancelAsync(long invoiceNo, string? reason, CancellationToken ct = default)
    {
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (inv.Status == StCancelled) throw new ValidationException("Invoice is already cancelled");

        if (inv.Status == StPosted && inv.DueAmount > 0)
        {
            await _arLedger.CreditAsync(inv.CustomerNo, inv.DueAmount, "CANCEL_INVOICE", inv.InvoiceNo, inv.InvoiceId, $"Cancellation of {inv.InvoiceId}", ct);
        }

        inv.Status = StCancelled;
        inv.CancelledBy = _ctx.CurrentUserNo();
        inv.CancelledAt = DateTime.UtcNow;
        inv.CancelReason = reason;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(invoiceNo, ct);
    }

    public async Task DeleteAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be deleted");

        inv.IsDeleted = 1; inv.IsActive = 0;
        inv.DeletedBy = _ctx.CurrentUserNo(); inv.DeletedAt = DateTime.UtcNow;

        var lines = await _db.SalInvoiceDtls.Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var l in lines) { l.IsDeleted = 1; l.IsActive = 0; l.DeletedBy = _ctx.CurrentUserNo(); l.DeletedAt = DateTime.UtcNow; }

        await _db.SaveChangesAsync(ct);
    }

    private async Task ReplaceLinesAsync(long invoiceNo, List<Sal1001LineDto> lines, CancellationToken ct)
    {
        var existing = await _db.SalInvoiceDtls.Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var e in existing) { e.IsDeleted = 1; e.DeletedBy = _ctx.CurrentUserNo(); e.DeletedAt = DateTime.UtcNow; }

        foreach (var dto in lines)
        {
            var lineTotal = dto.Quantity * dto.UnitPrice;
            var line = new SalInvoiceDtl
            {
                InvoiceNo = invoiceNo,
                ItemNo = dto.ItemNo,
                UomNo = dto.UomNo,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                UnitCost = dto.UnitCost,
                LineTotal = lineTotal,
                DiscountPercent = dto.DiscountPercent,
                DiscountAmount = dto.DiscountAmount,
                TaxableAmount = lineTotal - dto.DiscountAmount,
                TaxPercent = dto.TaxPercent,
                TaxAmount = dto.TaxAmount,
                NetAmount = lineTotal - dto.DiscountAmount + dto.TaxAmount,
                Remarks = dto.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.SalInvoiceDtls.Add(line);
        }
    }

    private async Task<List<Sal1001LineDto>> GetLinesAsync(long invoiceNo, CancellationToken ct)
    {
        return await _db.SalInvoiceDtls
            .AsNoTracking()
            .Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0)
            .Select(l => new Sal1001LineDto
            {
                InvoiceDtlNo = l.InvoiceDtlNo,
                ItemNo = l.ItemNo,
                UomNo = l.UomNo,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                UnitCost = l.UnitCost,
                LineTotal = l.LineTotal,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxableAmount = l.TaxableAmount,
                TaxPercent = l.TaxPercent,
                TaxAmount = l.TaxAmount,
                NetAmount = l.NetAmount,
                Remarks = l.Remarks
            })
            .ToListAsync(ct);
    }

    private static Sal1001InvoiceDto ToDto(SalInvoice i, string? customerName, List<Sal1001LineDto>? lines) => new()
    {
        InvoiceNo = i.InvoiceNo,
        InvoiceId = i.InvoiceId,
        InvoiceDate = i.InvoiceDate,
        SaleType = i.SaleType,
        CustomerNo = i.CustomerNo,
        CustomerName = customerName,
        WarehouseNo = i.WarehouseNo,
        Status = i.Status,
        SubTotal = i.SubTotal,
        LineDiscountTotal = i.LineDiscountTotal,
        TaxableAmount = i.TaxableAmount,
        TaxAmount = i.TaxAmount,
        ShippingCharge = i.ShippingCharge,
        RoundOff = i.RoundOff,
        GrandTotal = i.GrandTotal,
        PaidAmount = i.PaidAmount,
        DueAmount = i.DueAmount,
        TotalCost = i.TotalCost,
        PaymentStatus = i.PaymentStatus,
        DueDate = i.DueDate,
        FinYearNo = i.FinYearNo,
        ApprovalRequestNo = i.ApprovalRequestNo,
        Remarks = i.Remarks,
        IsActive = i.IsActive,
        RowVersion = i.RowVersion,
        Lines = lines ?? new()
    };
}
