using AidlyErp.Inv.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Engine;
using AidlyErp.Inv.Domain;

namespace AidlyErp.Inv.Application.Services;

public interface IInv1102Service
{
    Task<Inv1102AdjustmentDto> PostAdjustmentAsync(Inv1102AdjustmentDto dto, CancellationToken ct = default);
}

public class Inv1102Service : IInv1102Service
{
    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvStockPostingService _postingService;

    public Inv1102Service(IInvDbContext db, ICompanyBranchContext ctx, IInvStockPostingService postingService)
    {
        _db = db;
        _ctx = ctx;
        _postingService = postingService;
    }

    public async Task<Inv1102AdjustmentDto> PostAdjustmentAsync(Inv1102AdjustmentDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Adjustment lines are required");

        var adj = new InvStockAdjustment
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            AdjustmentId = $"ADJ-{DateTime.UtcNow:yyyyMMddHHmmss}",
            AdjustmentDate = dto.AdjustmentDate != default ? dto.AdjustmentDate : DateTime.UtcNow.Date,
            WarehouseNo = dto.WarehouseNo,
            Status = 2,
            Remarks = dto.Remarks,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.InvStockAdjustments.Add(adj);
        await _db.SaveChangesAsync(ct);
        adj.AdjustmentId = $"ADJ-{adj.AdjustmentNo:D6}";

        var legs = new List<StockPostingLeg>();
        int lineNo = 1;
        foreach (var l in dto.Lines)
        {
            var dtl = new InvStockAdjustmentDtl
            {
                AdjustmentNo = adj.AdjustmentNo,
                ItemNo = l.ProductNo,
                UomNo = l.UomNo,
                Qty = l.Quantity,
                AdjustmentType = l.AdjustmentType,
                UnitCost = l.UnitCost,
                Reason = l.Reason,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.InvStockAdjustmentDtls.Add(dtl);

            if (l.AdjustmentType == 1)
                legs.Add(StockPostingLeg.In(dto.WarehouseNo, l.ProductNo, null, null, l.Quantity, 4, lineNo++, l.UnitCost));
            else
                legs.Add(StockPostingLeg.Out(dto.WarehouseNo, l.ProductNo, null, null, l.Quantity, 4, lineNo++));
        }

        await _db.SaveChangesAsync(ct);

        var cmd = new StockPostingCommand(companyNo, branchNo, 4, adj.AdjustmentId, adj.AdjustmentNo, adj.AdjustmentDate, null, null, false, legs);
        await _postingService.PostAsync(cmd, ct);

        dto.AdjustmentNo = adj.AdjustmentNo;
        dto.AdjustmentId = adj.AdjustmentId;
        dto.Status = 2;
        return dto;
    }
}
