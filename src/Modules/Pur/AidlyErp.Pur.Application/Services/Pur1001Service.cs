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

/// <summary>
/// PUR_1001 Supplier Management — full port of Java Pur1001Service.
/// Company-scoped master CRUD. current_payable is derived (AP ledger) and never written by this form.
/// Delete is blocked while a payable remains.
/// </summary>
public class Pur1001Service : IPur1001Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    private static readonly HashSet<short> Types = [1, 2, 3, 4, 5];
    private static readonly HashSet<short> Terms = [1, 2, 3, 4];

    public Pur1001Service(IPurDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Pur1001SupplierDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.PurSuppliers.AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == 0)
            .OrderBy(s => s.SupplierNo)
            .Select(s => new Pur1001SupplierDto
            {
                SupplierNo = s.SupplierNo,
                SupplierCode = s.SupplierId,
                SupplierName = s.SupplierName,
                SupplierType = s.SupplierType,
                ContactPerson = s.ContactPerson,
                Email = s.Email,
                Phone = s.PhoneNo,
                Mobile = s.MobileNo,
                Address = s.AddressLine1,
                TaxNumber = s.VatRegNo,
                PaymentTerms = s.PaymentTerms,
                CreditDays = s.CreditDays,
                CreditLimit = s.CreditLimit,
                OpeningBalance = s.OpeningBalance,
                CurrentBalance = s.CurrentPayable,
                BranchNo = s.BranchNo,
                IsActive = s.IsActive ?? 0
            })
            .ToListAsync(ct);
    }

    public async Task<Pur1001SupplierDto> GetDetailAsync(long supplierNo, CancellationToken ct = default)
    {
        var s = await RequireAsync(supplierNo, ct);
        return ToDto(s);
    }

    public async Task<Pur1001SupplierDto> SaveAsync(Pur1001SupplierDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        long branchNo = dto.BranchNo ?? _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context required");

        Validate(dto);

        PurSupplier s;
        if (dto.SupplierNo.HasValue && dto.SupplierNo.Value > 0)
        {
            // Update
            s = await _db.PurSuppliers.FirstOrDefaultAsync(x => x.SupplierNo == dto.SupplierNo.Value && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Supplier not found: {dto.SupplierNo}");
            if (s.CompanyNo != companyNo) throw new ValidationException("Supplier belongs to another company");

            // Duplicate supplier_id check (scoped to company)
            if (!string.IsNullOrWhiteSpace(dto.SupplierCode) && dto.SupplierCode.Trim() != s.SupplierId)
            {
                bool dup = await _db.PurSuppliers.AnyAsync(x =>
                    x.SupplierId == dto.SupplierCode.Trim() && x.CompanyNo == companyNo
                    && x.SupplierNo != s.SupplierNo && x.IsDeleted == 0, ct);
                if (dup) throw new ValidationException($"Supplier ID already exists: {dto.SupplierCode}");
                s.SupplierId = dto.SupplierCode.Trim();
            }

            // Duplicate mobile check (scoped to company)
            if (!string.IsNullOrWhiteSpace(dto.Mobile) && dto.Mobile.Trim() != s.MobileNo)
            {
                bool dup = await _db.PurSuppliers.AnyAsync(x =>
                    x.MobileNo == dto.Mobile.Trim() && x.CompanyNo == companyNo
                    && x.SupplierNo != s.SupplierNo && x.IsDeleted == 0, ct);
                if (dup) throw new ValidationException("Mobile number already used by another supplier");
            }

            Apply(s, dto);
            s.UpdatedBy = _ctx.CurrentUserNo(); s.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create
            // Duplicate supplier_id check
            if (!string.IsNullOrWhiteSpace(dto.SupplierCode))
            {
                bool dup = await _db.PurSuppliers.AnyAsync(x =>
                    x.SupplierId == dto.SupplierCode.Trim() && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);
                if (dup) throw new ValidationException($"Supplier ID already exists: {dto.SupplierCode}");
            }

            // Duplicate mobile check
            if (!string.IsNullOrWhiteSpace(dto.Mobile))
            {
                bool dup = await _db.PurSuppliers.AnyAsync(x =>
                    x.MobileNo == dto.Mobile.Trim() && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);
                if (dup) throw new ValidationException("Mobile number already used by another supplier");
            }

            s = new PurSupplier
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                SupplierId = "TEMP",
                CurrentPayable = 0,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            Apply(s, dto);
            _db.PurSuppliers.Add(s);
            await _db.SaveChangesAsync(ct);

            // Auto-generate supplier_id
            if (s.SupplierId == "TEMP")
            {
                s.SupplierId = $"SUP-{s.SupplierNo:D6}";
                await _db.SaveChangesAsync(ct);
            }
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task DeleteAsync(long supplierNo, CancellationToken ct = default)
    {
        var s = await _db.PurSuppliers.FirstOrDefaultAsync(x => x.SupplierNo == supplierNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Supplier not found: {supplierNo}");

        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        if (s.CompanyNo != companyNo) throw new ValidationException("Supplier belongs to another company");

        // Delete guard — cannot delete supplier with outstanding payable
        if (s.CurrentPayable != 0)
            throw new ValidationException("Cannot delete a supplier with an outstanding payable — settle it or set inactive");

        s.IsDeleted = 1; s.IsActive = 0;
        s.DeletedBy = _ctx.CurrentUserNo(); s.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static void Validate(Pur1001SupplierDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SupplierName))
            throw new ValidationException("Supplier name is required");
        if (dto.SupplierType.HasValue && !Types.Contains(dto.SupplierType.Value))
            throw new ValidationException("Invalid supplier type");
        if (dto.PaymentTerms.HasValue && !Terms.Contains(dto.PaymentTerms.Value))
            throw new ValidationException("Invalid payment terms");
        if (dto.CreditLimit.HasValue && dto.CreditLimit.Value < 0)
            throw new ValidationException("Credit limit cannot be negative");
        if (dto.CreditDays.HasValue && dto.CreditDays.Value < 0)
            throw new ValidationException("Credit days cannot be negative");
    }

    private static void Apply(PurSupplier s, Pur1001SupplierDto d)
    {
        s.SupplierName = d.SupplierName!.Trim();
        s.SupplierNameNls = d.SupplierNameNls;
        s.SupplierType = d.SupplierType;
        s.ContactPerson = d.ContactPerson;
        s.MobileNo = BlankToNull(d.Mobile);
        s.AltMobileNo = d.AltMobileNo;
        s.PhoneNo = d.Phone;
        s.Email = d.Email;
        s.Website = d.Website;
        s.AddressLine1 = d.Address;
        s.AddressLine2 = d.AddressLine2;
        s.City = d.City;
        s.StateProvince = d.StateProvince;
        s.PostCode = d.PostCode;
        s.CountryCode = d.CountryCode;
        s.VatRegNo = d.TaxNumber;
        s.TinNo = d.TinNo;
        s.BinNo = d.BinNo;
        s.TradeLicenseNo = d.TradeLicenseNo;
        s.PaymentTerms = d.PaymentTerms ?? 1;
        s.CreditDays = d.CreditDays ?? 0;
        s.CreditLimit = d.CreditLimit ?? 0;
        s.DefaultCurrencyNo = d.DefaultCurrencyNo;
        s.DefaultVatTaxNo = d.DefaultVatTaxNo;
        s.OpeningBalance = d.OpeningBalance;
        s.OpeningBalanceDate = d.OpeningBalanceDate;
        s.BankName = d.BankName;
        s.BankAccountNo = d.BankAccountNo;
        s.BankBranch = d.BankBranch;
        s.RoutingNo = d.RoutingNo;
        s.MfsProvider = d.MfsProvider;
        s.MfsNumber = d.MfsNumber;
        s.LeadTimeDays = d.LeadTimeDays;
        s.Rating = d.Rating;
        s.ImagePath = d.ImagePath;
        s.Remarks = d.Remarks;
        s.IsActive = d.IsActive > 0 ? d.IsActive : (short)1;
    }

    private static Pur1001SupplierDto ToDto(PurSupplier s) => new()
    {
        SupplierNo = s.SupplierNo,
        SupplierCode = s.SupplierId,
        SupplierName = s.SupplierName,
        SupplierNameNls = s.SupplierNameNls,
        SupplierType = s.SupplierType,
        ContactPerson = s.ContactPerson,
        Mobile = s.MobileNo,
        AltMobileNo = s.AltMobileNo,
        Phone = s.PhoneNo,
        Email = s.Email,
        Website = s.Website,
        Address = s.AddressLine1,
        AddressLine2 = s.AddressLine2,
        City = s.City,
        StateProvince = s.StateProvince,
        PostCode = s.PostCode,
        CountryCode = s.CountryCode,
        TaxNumber = s.VatRegNo,
        TinNo = s.TinNo,
        BinNo = s.BinNo,
        TradeLicenseNo = s.TradeLicenseNo,
        PaymentTerms = s.PaymentTerms,
        CreditDays = s.CreditDays,
        CreditLimit = s.CreditLimit,
        DefaultCurrencyNo = s.DefaultCurrencyNo,
        DefaultVatTaxNo = s.DefaultVatTaxNo,
        OpeningBalance = s.OpeningBalance,
        OpeningBalanceDate = s.OpeningBalanceDate,
        CurrentBalance = s.CurrentPayable,
        BankName = s.BankName,
        BankAccountNo = s.BankAccountNo,
        BankBranch = s.BankBranch,
        RoutingNo = s.RoutingNo,
        MfsProvider = s.MfsProvider,
        MfsNumber = s.MfsNumber,
        LeadTimeDays = s.LeadTimeDays,
        Rating = s.Rating,
        ImagePath = s.ImagePath,
        Remarks = s.Remarks,
        BranchNo = s.BranchNo,
        IsActive = s.IsActive ?? 0,
        RowVersion = s.RowVersion
    };

    private async Task<PurSupplier> RequireAsync(long supplierNo, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        var s = await _db.PurSuppliers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SupplierNo == supplierNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Supplier not found: {supplierNo}");
        if (s.CompanyNo != companyNo) throw new ValidationException("Supplier belongs to another company");
        return s;
    }

    private static string? BlankToNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
