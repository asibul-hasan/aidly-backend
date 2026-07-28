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

public interface IPur1104Service
{
    Task<List<Pur1104PaymentDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1104PaymentDto> SaveAsync(Pur1104PaymentDto dto, CancellationToken ct = default);
    Task<Pur1104PaymentDto> PostAsync(long paymentNo, CancellationToken ct = default);
}

public class Pur1104Service : IPur1104Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IPurApLedgerService _apLedger;

    public Pur1104Service(IPurDbContext db, ICompanyBranchContext ctx, IPurApLedgerService apLedger)
    {
        _db = db;
        _ctx = ctx;
        _apLedger = apLedger;
    }

    public async Task<List<Pur1104PaymentDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var sups = await _db.PurSuppliers.AsNoTracking().Where(s => s.IsDeleted == 0).ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        var list = await _db.PurPayments
            .AsNoTracking()
            .Where(p => p.CompanyNo == companyNo && p.IsDeleted == 0)
            .OrderByDescending(p => p.PaymentNo)
            .ToListAsync(ct);

        return list.Select(p => new Pur1104PaymentDto
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
            Remarks = p.Remarks
        }).ToList();
    }

    public async Task<Pur1104PaymentDto> SaveAsync(Pur1104PaymentDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        long branchNo = _ctx.CurrentBranchNo() ?? 1;

        if (dto.SupplierNo <= 0 || dto.Amount <= 0) throw new ValidationException("Valid supplier and payment amount required");

        var p = new PurPayment
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            PaymentId = $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}",
            PaymentDate = dto.PaymentDate != default ? dto.PaymentDate : DateTime.UtcNow.Date,
            SupplierNo = dto.SupplierNo,
            PaymentMethod = dto.PaymentMethod,
            Amount = dto.Amount,
            AllocatedAmount = dto.AllocatedAmount,
            UnallocatedAmount = dto.Amount - dto.AllocatedAmount,
            Status = 1, // Draft
            Remarks = dto.Remarks,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };

        _db.PurPayments.Add(p);
        await _db.SaveChangesAsync(ct);
        p.PaymentId = $"PAY-{p.PaymentNo:D6}";
        await _db.SaveChangesAsync(ct);

        dto.PaymentNo = p.PaymentNo;
        dto.PaymentId = p.PaymentId;
        return dto;
    }

    public async Task<Pur1104PaymentDto> PostAsync(long paymentNo, CancellationToken ct = default)
    {
        var p = await _db.PurPayments.FirstOrDefaultAsync(x => x.PaymentNo == paymentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Payment not found: {paymentNo}");

        if (p.Status == 2) return new Pur1104PaymentDto { PaymentNo = p.PaymentNo, Status = 2 };

        await _apLedger.DebitAsync(p.SupplierNo, p.Amount, "SUPPLIER_PAYMENT", p.PaymentNo, p.PaymentId, $"Supplier Payment {p.PaymentId}", ct);

        p.Status = 2; // Posted
        await _db.SaveChangesAsync(ct);
        return new Pur1104PaymentDto { PaymentNo = p.PaymentNo, Status = 2 };
    }
}
