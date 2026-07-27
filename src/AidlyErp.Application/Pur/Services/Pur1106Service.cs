using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Pur.Dto;
using AidlyErp.Domain.Pur;

namespace AidlyErp.Application.Pur.Services;

/// <summary>
/// PUR_1106 Landed Cost — port of Java Pur1106Service.
/// Captures additional costs (freight, customs, etc.) and allocates them to purchase invoices.
/// </summary>
public interface IPur1106Service
{
    Task<List<Pur1106LandedCostDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1106LandedCostDto> GetDetailAsync(long landedCostNo, CancellationToken ct = default);
    Task<Pur1106LandedCostDto> SaveAsync(Pur1106LandedCostDto dto, CancellationToken ct = default);
    Task DeleteAsync(long landedCostNo, CancellationToken ct = default);
}

public class Pur1106Service : IPur1106Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Pur1106Service(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Pur1106LandedCostDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        return await _db.PurLandedCosts.AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.BranchNo == branchNo && c.IsDeleted == 0)
            .OrderByDescending(c => c.LandedCostNo)
            .Select(c => new Pur1106LandedCostDto
            {
                LandedCostNo = c.LandedCostNo,
                CostDate = c.CostDate,
                CostType = c.CostType,
                Amount = c.Amount,
                Status = c.Status
            })
            .ToListAsync(ct);
    }

    public async Task<Pur1106LandedCostDto> GetDetailAsync(long landedCostNo, CancellationToken ct = default)
    {
        var cost = await RequireAsync(landedCostNo, ct);

        var allocs = await _db.PurLandedCostAllocs.AsNoTracking()
            .Where(a => a.LandedCostNo == landedCostNo && a.IsDeleted == 0)
            .OrderBy(a => a.AllocNo)
            .ToListAsync(ct);

        return new Pur1106LandedCostDto
        {
            LandedCostNo = cost.LandedCostNo,
            CostDate = cost.CostDate,
            CostType = cost.CostType,
            Amount = cost.Amount,
            Status = cost.Status,
            Allocations = allocs.Select(a => new Pur1106AllocDto
            {
                AllocNo = a.AllocNo,
                LandedCostNo = a.LandedCostNo,
                InvoiceNo = a.InvoiceNo,
                AllocatedAmount = a.AllocatedAmount
            }).ToList()
        };
    }

    public async Task<Pur1106LandedCostDto> SaveAsync(Pur1106LandedCostDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.Amount <= 0) throw new ValidationException("Amount must be greater than zero");
        if (dto.Allocations == null || dto.Allocations.Count == 0) throw new ValidationException("Add at least one allocation");

        PurLandedCost cost;
        if (dto.LandedCostNo.HasValue && dto.LandedCostNo.Value > 0)
        {
            cost = await RequireAsync(dto.LandedCostNo.Value, ct);
            if (cost.Status != 1) throw new ValidationException("Only a Draft landed cost can be edited");
        }
        else
        {
            cost = new PurLandedCost
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                Status = 1,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.PurLandedCosts.Add(cost);
        }

        cost.CostDate = dto.CostDate;
        cost.CostType = dto.CostType ?? "FREIGHT";
        cost.Amount = dto.Amount;
        cost.UpdatedBy = _ctx.CurrentUserNo();
        cost.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Recompute allocations.
        var existing = await _db.PurLandedCostAllocs.Where(a => a.LandedCostNo == cost.LandedCostNo && a.IsDeleted == 0).ToListAsync(ct);
        foreach (var alloc in existing) alloc.PerformSoftDelete(_ctx.CurrentUserNo());

        foreach (var a in dto.Allocations)
        {
            if (a.InvoiceNo <= 0 || a.AllocatedAmount <= 0) continue;
            var alloc = new PurLandedCostAlloc
            {
                LandedCostNo = cost.LandedCostNo,
                InvoiceNo = a.InvoiceNo,
                AllocatedAmount = a.AllocatedAmount,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.PurLandedCostAllocs.Add(alloc);
        }

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(cost.LandedCostNo, ct);
    }

    public async Task DeleteAsync(long landedCostNo, CancellationToken ct = default)
    {
        var cost = await RequireAsync(landedCostNo, ct);
        if (cost.Status != 1) throw new ValidationException("Only a Draft landed cost can be deleted");

        var allocs = await _db.PurLandedCostAllocs.Where(a => a.LandedCostNo == landedCostNo && a.IsDeleted == 0).ToListAsync(ct);
        foreach (var alloc in allocs) alloc.PerformSoftDelete(_ctx.CurrentUserNo());

        cost.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PurLandedCost> RequireAsync(long landedCostNo, CancellationToken ct)
    {
        var companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var cost = await _db.PurLandedCosts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.LandedCostNo == landedCostNo && c.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Landed cost not found: landedCostNo={landedCostNo}");
        if (cost.CompanyNo != companyNo) throw new ValidationException("Landed cost belongs to another company");
        return cost;
    }
}
