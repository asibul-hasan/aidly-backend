using AidlyErp.Inv.Contracts;
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

public interface IInv2003Service
{
    Task<Inv2003TransferDto> PostTransferAsync(Inv2003TransferDto dto, CancellationToken ct = default);
}

public class Inv2003Service : IInv2003Service
{
    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvStockPostingService _postingService;

    public Inv2003Service(IInvDbContext db, ICompanyBranchContext ctx, IInvStockPostingService postingService)
    {
        _db = db;
        _ctx = ctx;
        _postingService = postingService;
    }

    public async Task<Inv2003TransferDto> PostTransferAsync(Inv2003TransferDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.FromWarehouseNo <= 0 || dto.ToWarehouseNo <= 0) throw new ValidationException("Source and destination warehouses are required");
        if (dto.FromWarehouseNo == dto.ToWarehouseNo) throw new ValidationException("Source and destination warehouses cannot be the same");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Transfer lines are required");

        var tr = new InvStockTransfer
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            TransferId = $"TRF-{DateTime.UtcNow:yyyyMMddHHmmss}",
            TransferDate = dto.TransferDate != default ? dto.TransferDate : DateTime.UtcNow.Date,
            FromWarehouseNo = dto.FromWarehouseNo,
            ToWarehouseNo = dto.ToWarehouseNo,
            Status = 2,
            Remarks = dto.Remarks,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.InvStockTransfers.Add(tr);
        await _db.SaveChangesAsync(ct);
        tr.TransferId = $"TRF-{tr.TransferNo:D6}";

        var legs = new List<StockPostingLeg>();
        int lineNo = 1;
        foreach (var l in dto.Lines)
        {
            var dtl = new InvStockTransferDtl
            {
                TransferNo = tr.TransferNo,
                ItemNo = l.ProductNo,
                UomNo = l.UomNo,
                Qty = l.Quantity,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.InvStockTransferDtls.Add(dtl);

            legs.Add(StockPostingLeg.Out(dto.FromWarehouseNo, l.ProductNo, null, null, l.Quantity, 3, lineNo));
            legs.Add(StockPostingLeg.In(dto.ToWarehouseNo, l.ProductNo, null, null, l.Quantity, 3, lineNo++, 0m));
        }

        await _db.SaveChangesAsync(ct);

        var cmd = new StockPostingCommand(companyNo, branchNo, 3, tr.TransferId, tr.TransferNo, tr.TransferDate, null, null, false, legs);
        await _postingService.PostAsync(cmd, ct);

        dto.TransferNo = tr.TransferNo;
        dto.TransferId = tr.TransferId;
        dto.Status = 2;
        return dto;
    }
}
