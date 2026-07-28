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

public interface IPur1001Service
{
    Task<List<Pur1001SupplierDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1001SupplierDto> GetDetailAsync(long supplierNo, CancellationToken ct = default);
    Task<Pur1001SupplierDto> SaveAsync(Pur1001SupplierDto dto, CancellationToken ct = default);
    Task DeleteAsync(long supplierNo, CancellationToken ct = default);
}

public class Pur1001Service : IPur1001Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Pur1001Service(IPurDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<Pur1001SupplierDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.PurSuppliers.AsNoTracking().Where(s => s.CompanyNo == companyNo && s.IsDeleted == 0).OrderBy(s => s.SupplierName)
            .Select(s => ToDto(s)).ToListAsync(ct);
    }

    public async Task<Pur1001SupplierDto> GetDetailAsync(long supplierNo, CancellationToken ct = default)
    {
        var s = await _db.PurSuppliers.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierNo == supplierNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Supplier not found: {supplierNo}");
        return ToDto(s);
    }

    public async Task<Pur1001SupplierDto> SaveAsync(Pur1001SupplierDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        if (string.IsNullOrWhiteSpace(dto.SupplierName)) throw new ValidationException("Supplier name is required");

        PurSupplier s;
        if (dto.SupplierNo.HasValue && dto.SupplierNo.Value > 0)
        {
            s = await _db.PurSuppliers.FirstOrDefaultAsync(x => x.SupplierNo == dto.SupplierNo.Value && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Supplier not found: {dto.SupplierNo}");
            s.SupplierName = dto.SupplierName.Trim();
            s.CompanyName = dto.CompanyName;
            s.Email = dto.Email;
            s.Phone = dto.Phone;
            s.Mobile = dto.Mobile;
            s.Address = dto.Address;
            s.TaxNumber = dto.TaxNumber;
            s.GlAccountNo = dto.GlAccountNo;
            s.IsActive = dto.IsActive;
            s.UpdatedBy = _ctx.CurrentUserNo(); s.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            s = new PurSupplier
            {
                CompanyNo = companyNo,
                BranchNo = dto.BranchNo ?? _ctx.CurrentBranchNo(),
                SupplierCode = string.IsNullOrWhiteSpace(dto.SupplierCode) ? $"SUP{DateTime.UtcNow:yyyyMMddHHmmss}" : dto.SupplierCode.Trim().ToUpperInvariant(),
                SupplierName = dto.SupplierName.Trim(),
                CompanyName = dto.CompanyName,
                Email = dto.Email,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                Address = dto.Address,
                TaxNumber = dto.TaxNumber,
                OpeningBalance = dto.OpeningBalance,
                CurrentBalance = dto.OpeningBalance,
                GlAccountNo = dto.GlAccountNo,
                Status = "ACTIVE",
                IsActive = dto.IsActive, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurSuppliers.Add(s);
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task DeleteAsync(long supplierNo, CancellationToken ct = default)
    {
        var s = await _db.PurSuppliers.FirstOrDefaultAsync(x => x.SupplierNo == supplierNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Supplier not found: {supplierNo}");
        s.IsDeleted = 1; s.IsActive = 0; s.DeletedBy = _ctx.CurrentUserNo(); s.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Pur1001SupplierDto ToDto(PurSupplier s) => new()
    {
        SupplierNo = s.SupplierNo,
        SupplierCode = s.SupplierCode,
        SupplierName = s.SupplierName,
        CompanyName = s.CompanyName,
        Email = s.Email,
        Phone = s.Phone,
        Mobile = s.Mobile,
        Address = s.Address,
        TaxNumber = s.TaxNumber,
        OpeningBalance = s.OpeningBalance,
        CurrentBalance = s.CurrentBalance,
        GlAccountNo = s.GlAccountNo,
        BranchNo = s.BranchNo,
        IsActive = s.IsActive,
        RowVersion = s.RowVersion
    };
}
