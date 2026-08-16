using AidlyErp.Inv.Contracts;
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
/// PUR_1101 Purchase Order — port of Java Pur1101Service.
/// A procurement commitment with an approval gate. No stock or GL impact (those happen at receipt).
/// </summary>
public interface IPur1101Service
{
    Task<List<Pur1101OrderDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1101OrderDto> GetDetailAsync(long orderNo, CancellationToken ct = default);
    Task<Pur1101OrderDto> SaveAsync(Pur1101OrderDto dto, CancellationToken ct = default);
    Task<Pur1101OrderDto> SubmitAsync(long orderNo, CancellationToken ct = default);
    Task<Pur1101OrderDto> ApproveAsync(long orderNo, CancellationToken ct = default);
    Task<Pur1101OrderDto> RejectAsync(long orderNo, CancellationToken ct = default);
    Task<Pur1101OrderDto> CancelAsync(long orderNo, string? reason, CancellationToken ct = default);
    Task DeleteAsync(long orderNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long orderNo, bool approved, CancellationToken ct = default);
}

public class Pur1101Service : IPur1101Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvCatalog _catalog;
    private readonly IApprovalService _approvalService;
    public const string DocType = "PUR_PO";
    private const short StDraft = 1, StSubmitted = 2, StApproved = 3, StCancelled = 7;

    private readonly IDocSequenceGenerator _docSeq;

    /// <summary>Doc-sequence key for purchase orders.</summary>
    private const string DocSeqKey = "PUR_ORDER";

    public Pur1101Service(IPurDbContext db, ICompanyBranchContext ctx, IApprovalService approvalService,
                          IInvCatalog catalog, IDocSequenceGenerator docSeq)
    {
        _db = db;
        _ctx = ctx;
        _approvalService = approvalService;
        _catalog = catalog;
        _docSeq = docSeq;
    }

    public async Task<List<Pur1101OrderDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        var orders = await _db.PurOrders.AsNoTracking()
            .Where(o => o.CompanyNo == companyNo && o.BranchNo == branchNo && o.IsDeleted == 0)
            .OrderByDescending(o => o.OrderNo)
            .ToListAsync(ct);

        var supplierNos = orders.Select(o => o.SupplierNo).Distinct().ToList();
        var suppliers = await _db.PurSuppliers.AsNoTracking()
            .Where(s => supplierNos.Contains(s.SupplierNo) && s.IsDeleted == 0)
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        var warehouseNos = orders.Select(o => o.WarehouseNo).Distinct().ToList();
        var warehouses = await _catalog.GetWarehouseNamesAsync(warehouseNos, ct);

        return orders.Select(o => ToHeaderDto(o, suppliers, warehouses)).ToList();
    }

    public async Task<Pur1101OrderDto> GetDetailAsync(long orderNo, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderNo, ct);
        var suppliers = await GetSupplierNamesAsync(ct);
        var warehouses = await GetWarehouseNamesAsync(ct);
        var dto = ToHeaderDto(order, suppliers, warehouses);

        var lines = await _db.PurOrderDtls.AsNoTracking()
            .Where(l => l.OrderNo == orderNo && l.IsDeleted == 0)
            .OrderBy(l => l.OrderDtlNo)
            .ToListAsync(ct);

        var productNos = lines.Select(l => l.ProductNo).Distinct().ToList();
        var products = await _catalog.GetProductNamesAsync(productNos, ct);
        var uoms = await _catalog.GetUomNamesAsync(lines.Select(l => l.UomNo).Distinct().ToList(), ct);

        dto.Lines = lines.Select(l => new Pur1101LineDto
        {
            OrderDtlNo = l.OrderDtlNo,
            ProductNo = l.ProductNo,
            ProductName = products.GetValueOrDefault(l.ProductNo),
            UomNo = l.UomNo,
            UomName = uoms.GetValueOrDefault(l.UomNo),
            OrderQty = l.OrderQty,
            // How much of this line has already arrived — the reason a buyer opens a PO.
            ReceivedQtyBase = l.ReceivedQtyBase,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal,
            Remarks = l.Remarks
        }).ToList();

        return dto;
    }

    public async Task<Pur1101OrderDto> SaveAsync(Pur1101OrderDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.SupplierNo <= 0) throw new ValidationException("Supplier is required");
        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Add at least one line");

        await RequireSupplierAsync(dto.SupplierNo, companyNo, ct);
        await RequireWarehouseAsync(dto.WarehouseNo, branchNo, ct);

        PurOrder order;
        if (dto.OrderNo.HasValue && dto.OrderNo.Value > 0)
        {
            order = await RequireAsync(dto.OrderNo.Value, ct);
            if (order.Status != StDraft) throw new ValidationException("Only a Draft PO can be edited");
        }
        else
        {
            // Was $"PO-{DateTime.UtcNow.Ticks}", which produced ids like PO-639224877667180407:
            // unreadable, unsortable by eye, and outside the numbering scheme every other document
            // uses. A purchase order is quoted to suppliers, so it needs a real sequence.
            string orderId = await _docSeq.NextAsync(companyNo, branchNo, DocSeqKey, "PO", 6, ct);

            order = new PurOrder
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                Status = StDraft,
                OrderId = orderId,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.PurOrders.Add(order);
        }

        order.SupplierNo = dto.SupplierNo;
        order.WarehouseNo = dto.WarehouseNo;
        order.OrderDate = dto.OrderDate;
        order.ExpectedDeliveryDate = dto.OrderDate.AddDays(30); // Default expected
        order.TaxAmount = dto.TaxAmount;
        order.Remarks = dto.Remarks;
        order.UpdatedBy = _ctx.CurrentUserNo();
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Recompute lines.
        await RecomputeLinesAsync(order, dto.Lines, ct);

        return await GetDetailAsync(order.OrderNo, ct);
    }

    public async Task<Pur1101OrderDto> SubmitAsync(long orderNo, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderNo, ct);
        if (order.Status != StDraft) throw new ValidationException("Only a Draft PO can be submitted");

        var lines = await _db.PurOrderDtls.Where(l => l.OrderNo == orderNo && l.IsDeleted == 0).ToListAsync(ct);
        if (lines.Count == 0) throw new ValidationException("Add at least one line before submitting");

        order.SubmittedBy = _ctx.CurrentUserNo();
        order.SubmittedAt = DateTime.UtcNow;

        if (order.ApprovalRequestNo is null)
        {
            var outcome = await _approvalService.RaiseAsync(DocType, order.OrderNo, order.OrderId, order.GrandTotal, ct);
            if (outcome.AutoApproved)
            {
                await ApplyApprovalOutcomeAsync(orderNo, true, ct);
            }
            else
            {
                order.Status = StSubmitted;
                order.ApprovalRequestNo = outcome.ApprovalRequestNo;
                order.UpdatedBy = _ctx.CurrentUserNo();
                order.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }
        else
        {
            // Already in the chain — advance the current step instead of raising a second request.
            await _approvalService.ActAsync(order.ApprovalRequestNo.Value, true, null, ct);
        }

        return await GetDetailAsync(orderNo, ct);
    }

    /// <summary>
    /// Advances the approval chain rather than stamping the order directly: the engine is what
    /// checks the acting user holds the current step's role, and it is what fires the completion
    /// event that calls <see cref="ApplyApprovalOutcomeAsync"/>.
    /// </summary>
    public async Task<Pur1101OrderDto> ApproveAsync(long orderNo, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderNo, ct);
        if (order.ApprovalRequestNo is null) throw new ValidationException("No approval request — submit first");

        await _approvalService.ActAsync(order.ApprovalRequestNo.Value, true, null, ct);
        return await GetDetailAsync(orderNo, ct);
    }

    public async Task<Pur1101OrderDto> RejectAsync(long orderNo, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderNo, ct);
        if (order.ApprovalRequestNo is null) throw new ValidationException("No approval request — submit first");

        await _approvalService.ActAsync(order.ApprovalRequestNo.Value, false, null, ct);
        return await GetDetailAsync(orderNo, ct);
    }

    public async Task<Pur1101OrderDto> CancelAsync(long orderNo, string? reason, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderNo, ct);
        if (order.Status == StCancelled) throw new ValidationException("Already cancelled");
        if (order.ReceivedValue > 0)
            throw new ValidationException("Goods already received against this PO — cannot cancel");
        order.Status = StCancelled;
        if (reason != null) order.Remarks = (order.Remarks != null ? order.Remarks + " | " : "") + "Cancelled: " + reason;
        order.UpdatedBy = _ctx.CurrentUserNo();
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(orderNo, ct);
    }

    public async Task DeleteAsync(long orderNo, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderNo, ct);
        if (order.Status != StDraft) throw new ValidationException("Only a Draft PO can be deleted — cancel it instead");

        var lines = await _db.PurOrderDtls.Where(l => l.OrderNo == orderNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var line in lines) line.PerformSoftDelete(_ctx.CurrentUserNo());

        order.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    public async Task ApplyApprovalOutcomeAsync(long orderNo, bool approved, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderNo, ct);
        if (approved)
        {
            order.Status = StApproved;
            order.ApprovedBy = _ctx.CurrentUserNo();
            order.ApprovedAt = DateTime.UtcNow;
        }
        else
        {
            order.Status = StDraft;
            order.ApprovalRequestNo = null;
        }
        order.UpdatedBy = _ctx.CurrentUserNo();
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task RecomputeLinesAsync(PurOrder order, List<Pur1101LineDto> rows, CancellationToken ct)
    {
        // Remove existing lines.
        var existing = await _db.PurOrderDtls.Where(l => l.OrderNo == order.OrderNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var line in existing) line.PerformSoftDelete(_ctx.CurrentUserNo());

        decimal subTotal = 0, discTotal = 0, tax = 0;
        int lineNo = 1;

        var live = rows.Where(r => r.ProductNo > 0 && r.OrderQty > 0).ToList();
        var products = await _catalog.GetProductsAsync(live.Select(r => r.ProductNo).Distinct().ToList(),
                                                       order.CompanyNo, ct);
        var uom = await PurUomConverter.LoadAsync(_catalog, products.Keys, ct);

        foreach (var r in live)
        {
            if (!products.TryGetValue(r.ProductNo, out var product))
                throw new NotFoundException($"Product not found: {r.ProductNo}");

            decimal qty = r.OrderQty;
            decimal unitPrice = r.UnitPrice;
            decimal gross = Math.Round(qty * unitPrice, 4);

            // Discount: use explicit amount if set, otherwise compute from pct
            decimal discPct = r.DiscountPct;
            decimal discAmt = r.DiscountAmount > 0
                ? r.DiscountAmount
                : Math.Round(gross * discPct / 100m, 4);

            decimal taxable = gross - discAmt;
            decimal taxPct = r.TaxRatePct;
            decimal lineTax = Math.Round(taxable * taxPct / 100m, 4);

            var detail = new PurOrderDtl
            {
                OrderNo = order.OrderNo,
                LineNo = lineNo++,
                ProductNo = product.ProductNo,
                VariantNo = r.VariantNo,
                UomNo = r.UomNo ?? product.BaseUomNo,
                OrderQty = qty,
                OrderQtyBase = uom.ToBaseQty(product, r.UomNo ?? product.BaseUomNo, qty),
                UnitPrice = unitPrice,
                DiscountPct = discPct,
                DiscountAmount = discAmt,
                VatTaxNo = r.VatTaxNo,
                TaxRatePct = taxPct,
                TaxAmount = lineTax,
                LineTotal = taxable + lineTax,
                Remarks = r.Remarks,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.PurOrderDtls.Add(detail);
            subTotal += gross;
            discTotal += discAmt;
            tax += lineTax;
        }

        if (subTotal == 0) throw new ValidationException("All lines are empty");

        order.SubTotal = subTotal;
        order.DiscountTotal = discTotal;
        order.TaxAmount = tax;
        order.GrandTotal = subTotal - discTotal + tax + order.ShippingEstimate;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Loads a PO <b>tracked</b>. It used to be AsNoTracking, which made every state transition a
    /// no-op: submit, approve, reject and cancel all mutated a detached entity, so SaveChanges
    /// wrote nothing and each endpoint still returned 200. A submitted PO stayed Draft and the
    /// approval chain could never start — the API reported success the whole way.
    ///
    /// <para>Reads that genuinely do not mutate use their own AsNoTracking projections; this
    /// helper is only reached from the write paths.</para>
    /// </summary>
    private async Task<PurOrder> RequireAsync(long orderNo, CancellationToken ct)
    {
        var companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var order = await _db.PurOrders
            .FirstOrDefaultAsync(o => o.OrderNo == orderNo && o.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"PO not found: orderNo={orderNo}");
        if (order.CompanyNo != companyNo) throw new ValidationException("PO belongs to another company");
        return order;
    }

    private async Task RequireSupplierAsync(long supplierNo, long companyNo, CancellationToken ct)
    {
        var supplier = await _db.PurSuppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SupplierNo == supplierNo && s.IsDeleted == 0, ct)
            ?? throw new NotFoundException("Supplier not found");
        if (supplier.CompanyNo != companyNo) throw new ValidationException("Supplier not in your company");
    }

    private async Task RequireWarehouseAsync(long warehouseNo, long branchNo, CancellationToken ct)
    {
        var warehouse = await _catalog.FindWarehouseAsync(warehouseNo, ct)
            ?? throw new NotFoundException("Warehouse not found");
        if (warehouse.BranchNo != branchNo) throw new ValidationException("Warehouse does not belong to your branch");
    }

    private async Task<Dictionary<long, string>> GetSupplierNamesAsync(CancellationToken ct)
    {
        var companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.PurSuppliers.AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == 0)
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);
    }

    private async Task<Dictionary<long, string>> GetWarehouseNamesAsync(CancellationToken ct)
    {
        var companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var branchNo = _ctx.CurrentBranchNo() ?? 0;
        var warehouses = await _catalog.ListWarehousesAsync(companyNo, branchNo, ct);
        return warehouses.ToDictionary(w => w.WarehouseNo, w => w.WarehouseName ?? string.Empty);
    }

    private static Pur1101OrderDto ToHeaderDto(PurOrder o, IReadOnlyDictionary<long, string> sup, IReadOnlyDictionary<long, string> wh) => new()
    {
        OrderNo = o.OrderNo,
        OrderId = o.OrderId,
        OrderDate = o.OrderDate,
        SupplierNo = o.SupplierNo,
        SupplierName = sup.GetValueOrDefault(o.SupplierNo),
        WarehouseNo = o.WarehouseNo,
        WarehouseName = wh.GetValueOrDefault(o.WarehouseNo),
        ExpectedDate = o.ExpectedDate,
        SubTotal = o.SubTotal,
        DiscountTotal = o.DiscountTotal,
        TaxAmount = o.TaxAmount,
        ShippingEstimate = o.ShippingEstimate,
        GrandTotal = o.GrandTotal,
        ReceivedValue = o.ReceivedValue,
        FinYearNo = o.FinYearNo,
        Status = o.Status,
        ApprovalRequestNo = o.ApprovalRequestNo,
        TermsNote = o.TermsNote,
        Remarks = o.Remarks,
        IsActive = o.IsActive ?? 0,
        RowVersion = o.RowVersion
    };
}
