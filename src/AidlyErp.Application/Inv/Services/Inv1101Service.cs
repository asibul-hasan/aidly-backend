using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Inv.Dto;
using AidlyErp.Application.Inv.Engine;

namespace AidlyErp.Application.Inv.Services;

public interface IInv1101Service
{
    Task<Inv1101OpeningDto> PostOpeningStockAsync(Inv1101OpeningDto dto, CancellationToken ct = default);
}

public class Inv1101Service : IInv1101Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvStockPostingService _postingService;

    public Inv1101Service(IApplicationDbContext db, ICompanyBranchContext ctx, IInvStockPostingService postingService)
    {
        _db = db;
        _ctx = ctx;
        _postingService = postingService;
    }

    public async Task<Inv1101OpeningDto> PostOpeningStockAsync(Inv1101OpeningDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Lines are required");

        var legs = dto.Lines.Select((l, idx) => StockPostingLeg.In(
            dto.WarehouseNo, l.ProductNo, l.VariantNo, null, l.Quantity, 1, idx + 1, l.UnitCost)).ToList();

        var cmd = new StockPostingCommand(
            companyNo, branchNo, 1, $"OPN-{DateTime.UtcNow:yyyyMMddHHmmss}", 1, dto.OpeningDate != default ? dto.OpeningDate : DateTime.UtcNow.Date, null, null, true, legs);

        await _postingService.PostAsync(cmd, ct);
        dto.Status = 2;
        return dto;
    }
}
