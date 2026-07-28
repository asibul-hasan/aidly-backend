using AidlyErp.Inv.Contracts;
using AidlyErp.Shared.Core;
using AidlyErp.Pur.Application.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Contracts;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Domain;

namespace AidlyErp.Pur.Application.Services;

public interface IPur1102Service
{
    Task<List<Pur1102InvoiceDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1102InvoiceDto> GetDetailAsync(long invoiceNo, CancellationToken ct = default);
    Task<Pur1102InvoiceDto> SaveAsync(Pur1102InvoiceDto dto, CancellationToken ct = default);
    Task<Pur1102InvoiceDto> PostAsync(long invoiceNo, CancellationToken ct = default);
    Task DeleteAsync(long invoiceNo, CancellationToken ct = default);
}

public class Pur1102Service : IPur1102Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IPurApLedgerService _apLedger;
    private readonly IInvStockPostingService _stockPostingService;

    private const short StDraft = 1, StPosted = 2, StCancelled = 3;

    public Pur1102Service(IPurDbContext db, ICompanyBranchContext ctx, IPurApLedgerService apLedger, IInvStockPostingService stockPostingService)
    {
        _db = db;
        _ctx = ctx;
        _apLedger = apLedger;
        _stockPostingService = stockPostingService;
    }

    public async Task<List<Pur1102InvoiceDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var sups = await _db.PurSuppliers.AsNoTracking().Where(s => s.IsDeleted == 0).ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        var list = await _db.PurInvoices
            .AsNoTracking()
            .Where(i => i.CompanyNo == companyNo && i.IsDeleted == 0)
            .OrderByDescending(i => i.InvoiceNo)
            .ToListAsync(ct);

        return list.Select(i => ToDto(i, sups.GetValueOrDefault(i.SupplierNo), null)).ToList();
    }

    public async Task<Pur1102InvoiceDto> GetDetailAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await _db.PurInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Purchase invoice not found: {invoiceNo}");

        var supName = await _db.PurSuppliers.Where(s => s.SupplierNo == inv.SupplierNo).Select(s => s.SupplierName).FirstOrDefaultAsync(ct);
        var lines = await GetLinesAsync(invoiceNo, ct);

        return ToDto(inv, supName, lines);
    }

    public async Task<Pur1102InvoiceDto> SaveAsync(Pur1102InvoiceDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = dto.BranchNo ?? _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.SupplierNo <= 0) throw new ValidationException("Supplier is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Invoice lines are required");

        decimal subTotal = dto.Lines.Sum(l => l.Quantity * l.UnitPrice);
        decimal taxAmount = dto.Lines.Sum(l => l.TaxAmount);
        decimal grandTotal = subTotal + taxAmount;

        PurInvoice inv;
        if (dto.InvoiceNo.HasValue && dto.InvoiceNo.Value > 0)
        {
            inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == dto.InvoiceNo.Value && i.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Invoice not found: {dto.InvoiceNo}");
            if (inv.Status != StDraft) throw new ValidationException("Only a Draft purchase invoice can be edited");

            inv.SubTotal = subTotal;
            inv.TaxAmount = taxAmount;
            inv.GrandTotal = grandTotal;
            inv.DueAmount = grandTotal - inv.PaidAmount;
            inv.DueDate = dto.DueDate;
            inv.Remarks = dto.Remarks;
            inv.UpdatedBy = _ctx.CurrentUserNo(); inv.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            inv = new PurInvoice
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                InvoiceId = $"PINV-{DateTime.UtcNow:yyyyMMddHHmmss}",
                InvoiceDate = dto.InvoiceDate != default ? dto.InvoiceDate : DateTime.UtcNow.Date,
                SupplierNo = dto.SupplierNo,
                WarehouseNo = dto.WarehouseNo > 0 ? dto.WarehouseNo : 1,
                OrderNo = dto.OrderNo,
                SubTotal = subTotal,
                TaxAmount = taxAmount,
                GrandTotal = grandTotal,
                PaidAmount = 0m,
                DueAmount = grandTotal,
                PaymentStatus = 1,
                Status = StDraft,
                DueDate = dto.DueDate ?? DateTime.UtcNow.AddDays(30),
                Remarks = dto.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurInvoices.Add(inv);
            await _db.SaveChangesAsync(ct);
            inv.InvoiceId = $"PINV-{inv.InvoiceNo:D6}";
        }

        await ReplaceLinesAsync(inv.InvoiceNo, dto.Lines, ct);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(inv.InvoiceNo, ct);
    }

    public async Task<Pur1102InvoiceDto> PostAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (inv.Status == StPosted) return await GetDetailAsync(invoiceNo, ct);
        if (inv.Status != StDraft) throw new ValidationException("Only a Draft purchase invoice can be posted");

        var lines = await _db.PurInvoiceDtls.Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0).ToListAsync(ct);

        var legs = lines.Select((l, idx) => StockPostingLeg.In(
            inv.WarehouseNo, l.ItemNo, null, null, l.Quantity, 1, idx + 1, l.UnitPrice)).ToList();

        var cmd = new StockPostingCommand(inv.CompanyNo, inv.BranchNo, 2, inv.InvoiceId, inv.InvoiceNo, inv.InvoiceDate, null, null, true, legs);
        await _stockPostingService.PostAsync(cmd, ct);

        await _apLedger.CreditAsync(inv.SupplierNo, inv.GrandTotal, "PURCHASE_INVOICE", inv.InvoiceNo, inv.InvoiceId, $"Purchase Invoice {inv.InvoiceId}", ct);

        var payload = new GlPostingPayload
        {
            VoucherDate = inv.InvoiceDate,
            Narration = $"Purchase Invoice {inv.InvoiceId}",
            BranchNo = inv.BranchNo,
            Legs = new List<GlPostingPayload.Leg>
            {
                new() { LegKey = "INVENTORY", Amount = inv.SubTotal, DrCr = "dr" },
                new() { LegKey = "VAT_INPUT", Amount = inv.TaxAmount, DrCr = "dr" },
                new() { LegKey = "PAYABLE", Amount = inv.GrandTotal, DrCr = "cr", PartyType = 2, PartyNo = inv.SupplierNo }
            }
        };

        var ev = new EventOutbox
        {
            CompanyNo = inv.CompanyNo,
            BranchNo = inv.BranchNo,
            EventType = "PurchaseInvoicePosted",
            AggregateType = "PurInvoice",
            AggregateId = inv.InvoiceNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1, CreatedAt = DateTime.UtcNow
        };
        _db.EventOutboxes.Add(ev);

        inv.Status = StPosted;
        inv.PostedBy = _ctx.CurrentUserNo();
        inv.PostedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(invoiceNo, ct);
    }

    public async Task DeleteAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be deleted");

        inv.IsDeleted = 1; inv.IsActive = 0;
        inv.DeletedBy = _ctx.CurrentUserNo(); inv.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ReplaceLinesAsync(long invoiceNo, List<Pur1102LineDto> lines, CancellationToken ct)
    {
        var existing = await _db.PurInvoiceDtls.Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var e in existing) { e.IsDeleted = 1; e.DeletedBy = _ctx.CurrentUserNo(); e.DeletedAt = DateTime.UtcNow; }

        foreach (var dto in lines)
        {
            var lineTotal = dto.Quantity * dto.UnitPrice;
            var line = new PurInvoiceDtl
            {
                InvoiceNo = invoiceNo,
                ItemNo = dto.ItemNo,
                UomNo = dto.UomNo,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                LineTotal = lineTotal,
                DiscountAmount = dto.DiscountAmount,
                TaxAmount = dto.TaxAmount,
                NetAmount = lineTotal - dto.DiscountAmount + dto.TaxAmount,
                Remarks = dto.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurInvoiceDtls.Add(line);
        }
    }

    private async Task<List<Pur1102LineDto>> GetLinesAsync(long invoiceNo, CancellationToken ct)
    {
        return await _db.PurInvoiceDtls
            .AsNoTracking()
            .Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0)
            .Select(l => new Pur1102LineDto
            {
                InvoiceDtlNo = l.InvoiceDtlNo,
                ItemNo = l.ItemNo,
                UomNo = l.UomNo,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
                DiscountAmount = l.DiscountAmount,
                TaxAmount = l.TaxAmount,
                NetAmount = l.NetAmount,
                Remarks = l.Remarks
            })
            .ToListAsync(ct);
    }

    private static Pur1102InvoiceDto ToDto(PurInvoice i, string? supName, List<Pur1102LineDto>? lines) => new()
    {
        InvoiceNo = i.InvoiceNo,
        InvoiceId = i.InvoiceId,
        InvoiceDate = i.InvoiceDate,
        SupplierNo = i.SupplierNo,
        SupplierName = supName,
        WarehouseNo = i.WarehouseNo,
        OrderNo = i.OrderNo,
        SubTotal = i.SubTotal,
        TaxAmount = i.TaxAmount,
        GrandTotal = i.GrandTotal,
        PaidAmount = i.PaidAmount,
        DueAmount = i.DueAmount,
        PaymentStatus = i.PaymentStatus,
        Status = i.Status,
        DueDate = i.DueDate,
        Remarks = i.Remarks,
        BranchNo = i.BranchNo,
        IsActive = i.IsActive,
        RowVersion = i.RowVersion,
        Lines = lines ?? new()
    };
}
