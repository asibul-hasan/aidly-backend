using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Inv.Dto;
using AidlyErp.Domain.Inv;

namespace AidlyErp.Application.Inv.Services;

public interface IInv2004Service
{
    Task<List<Inv2004BatchDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv2004BatchDto> SaveAsync(Inv2004BatchDto dto, CancellationToken ct = default);
    Task DeleteAsync(long batchNo, CancellationToken ct = default);
}

public class Inv2004Service : IInv2004Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Inv2004Service(IApplicationDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<Inv2004BatchDto>> GetListAsync(CancellationToken ct = default) =>
        await _db.InvBatches.AsNoTracking()
            .Where(b => b.IsDeleted == 0)
            .OrderBy(b => b.BatchNo)
            .Select(b => new Inv2004BatchDto
            {
                BatchNo = b.BatchNo,
                BatchCode = b.BatchNumber,
                ProductNo = b.ProductNo,
                MfgDate = b.MfgDate,
                ExpDate = b.ExpiryDate,
                QtyOnHand = b.Qty
            })
            .ToListAsync(ct);

    public async Task<Inv2004BatchDto> SaveAsync(Inv2004BatchDto dto, CancellationToken ct = default)
    {
        if (dto.BatchNo > 0)
        {
            var entity = await _db.InvBatches.FirstOrDefaultAsync(b => b.BatchNo == dto.BatchNo && b.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Batch not found: {dto.BatchNo}");
            if (dto.BatchCode != null) entity.BatchNumber = dto.BatchCode;
            entity.ProductNo = dto.ProductNo;
            entity.MfgDate = dto.MfgDate;
            entity.ExpiryDate = dto.ExpDate;
            entity.Qty = dto.QtyOnHand;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return MapToDto(entity);
        }

        if (string.IsNullOrWhiteSpace(dto.BatchCode)) throw new ValidationException("Batch code is required");
        var newBatch = new InvBatch
        {
            BatchNumber = dto.BatchCode,
            ProductNo = dto.ProductNo,
            MfgDate = dto.MfgDate,
            ExpiryDate = dto.ExpDate,
            Qty = dto.QtyOnHand,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.InvBatches.Add(newBatch);
        await _db.SaveChangesAsync(ct);
        return MapToDto(newBatch);
    }

    public async Task DeleteAsync(long batchNo, CancellationToken ct = default)
    {
        var entity = await _db.InvBatches.FirstOrDefaultAsync(b => b.BatchNo == batchNo && b.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Batch not found: {batchNo}");
        entity.IsDeleted = 1; entity.IsActive = 0;
        entity.DeletedBy = _ctx.CurrentUserNo(); entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Inv2004BatchDto MapToDto(InvBatch b) => new()
    {
        BatchNo = b.BatchNo,
        BatchCode = b.BatchNumber,
        ProductNo = b.ProductNo,
        MfgDate = b.MfgDate,
        ExpDate = b.ExpiryDate,
        QtyOnHand = b.Qty
    };
}
