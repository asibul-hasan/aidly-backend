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
    Task<Pur1101OrderDto> CancelAsync(long orderNo, string? reason, CancellationToken ct = default);
    Task DeleteAsync(long orderNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long orderNo, bool approved, CancellationToken ct = default);
}

public class Pur1101Service : IPur1101Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IApprovalService _approvalService;
    public const string DocType = "PUR_PO";
    private const short StDraft = 1, StSubmitted = 2, StApproved = 3, StCancelled = 7;

    public Pur1101Service(IPurDbContext db, ICompanyBranchContext ctx, IApprovalService approvalService)
    {
        _db = db;
        _ctx = ctx;
        _approvalService = approvalService;
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
        var warehouses = await _db.InvWarehouses.AsNoTracking()
            .Where(w => warehouseNos.Contains(w.WarehouseNo) && w.IsDeleted == 0)
            .ToDictionaryAsync(w => w.WarehouseNo, w => w.WarehouseName, ct);

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

        var productNos = lines.Select(l => l.ItemNo).Distinct().ToList();
        var products = await _db.InvProducts.AsNoTracking()
            .Where(p => productNos.Contains(p.ProductNo) && p.IsDeleted == 0)
            .ToDictionaryAsync(p => p.ProductNo, p => p.ProductName, ct);

        dto.Lines = lines.Select(l => new Pur1101LineDto
        {
            OrderDtlNo = l.OrderDtlNo,
            ItemNo = l.ItemNo,
            ItemName = products.GetValueOrDefault(l.ItemNo),
            UomNo = l.UomNo,
            Quantity = l.Quantity,
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
            order = new PurOrder
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                Status = StDraft,
                OrderId = $"PO-{DateTime.UtcNow.Ticks}",
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

        return await GetDetailAsync(orderNo, ct);
    }

    public async Task<Pur1101OrderDto> ApproveAsync(long orderNo, CancellationToken ct = default)
    {
        await ApplyApprovalOutcomeAsync(orderNo, true, ct);
        return await GetDetailAsync(orderNo, ct);
    }

    public async Task<Pur1101OrderDto> CancelAsync(long orderNo, string? reason, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderNo, ct);
        if (order.Status == StCancelled) throw new ValidationException("Already cancelled");
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

        decimal subTotal = 0, tax = 0;
        int lineNo = 1;
        foreach (var r in rows)
        {
            if (r.ItemNo <= 0 || r.Quantity <= 0) continue;

            var product = await _db.InvProducts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductNo == r.ItemNo && p.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Product not found: {r.ItemNo}");

            decimal lineTotal = r.Quantity * r.UnitPrice;
            decimal lineTax = lineTotal * 0; // Tax rate from DTO if available

            var detail = new PurOrderDtl
            {
                OrderNo = order.OrderNo,
                ItemNo = r.ItemNo,
                UomNo = r.UomNo,
                Quantity = r.Quantity,
                UnitPrice = r.UnitPrice,
                LineTotal = lineTotal + lineTax,
                Remarks = r.Remarks,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.PurOrderDtls.Add(detail);
            subTotal += lineTotal;
            tax += lineTax;
        }

        if (subTotal == 0) throw new ValidationException("All lines are empty");

        order.TotalAmount = subTotal;
        order.TaxAmount = tax;
        order.GrandTotal = subTotal + tax;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PurOrder> RequireAsync(long orderNo, CancellationToken ct)
    {
        var companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var order = await _db.PurOrders.AsNoTracking()
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
        var warehouse = await _db.InvWarehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.WarehouseNo == warehouseNo && w.IsDeleted == 0, ct)
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
        return await _db.InvWarehouses.AsNoTracking()
            .Where(w => w.CompanyNo == companyNo && w.BranchNo == branchNo && w.IsDeleted == 0)
            .ToDictionaryAsync(w => w.WarehouseNo, w => w.WarehouseName, ct);
    }

    private static Pur1101OrderDto ToHeaderDto(PurOrder o, Dictionary<long, string> sup, Dictionary<long, string> wh) => new()
    {
        OrderNo = o.OrderNo,
        OrderId = o.OrderId,
        OrderDate = o.OrderDate,
        SupplierNo = o.SupplierNo,
        SupplierName = sup.GetValueOrDefault(o.SupplierNo),
        WarehouseNo = o.WarehouseNo,
        WarehouseName = wh.GetValueOrDefault(o.WarehouseNo),
        TotalAmount = o.TotalAmount,
        TaxAmount = o.TaxAmount,
        GrandTotal = o.GrandTotal,
        Status = o.Status,
        ApprovalRequestNo = o.ApprovalRequestNo,
        Remarks = o.Remarks,
        IsActive = o.IsActive,
        RowVersion = o.RowVersion
    };
}
