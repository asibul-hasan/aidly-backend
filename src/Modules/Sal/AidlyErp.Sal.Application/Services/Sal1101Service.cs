using AidlyErp.Sal.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Domain;

namespace AidlyErp.Sal.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Sal1101Service — Customer Master
// Port of Java sal/service/Sal1101Service.java.
// ═══════════════════════════════════════════════════════════════════════════

public interface ISal1101Service
{
    Task<List<Sal1101CustomerDto>> GetListAsync(CancellationToken ct = default);
    Task<Sal1101CustomerDto> GetDetailAsync(long customerNo, CancellationToken ct = default);
    Task<Sal1101CustomerDto> SaveAsync(Sal1101CustomerDto dto, CancellationToken ct = default);
    Task DeleteAsync(long customerNo, CancellationToken ct = default);
}

public class Sal1101Service : ISal1101Service
{
    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IDocSequenceGenerator _docSeq;

    private const string DocType = "SAL_CUSTOMER";

    public Sal1101Service(ISalDbContext db, ICompanyBranchContext ctx, IDocSequenceGenerator docSeq)
    {
        _db = db;
        _ctx = ctx;
        _docSeq = docSeq;
    }

    public async Task<List<Sal1101CustomerDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;

        // Materialise BEFORE projecting: ToDto is a static method call, which EF Core
        // cannot translate inside an IQueryable — it throws rather than client-evaluating.
        var rows = await _db.SalCustomers
            .AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.IsDeleted == 0)
            .OrderBy(c => c.CustomerName)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Sal1101CustomerDto> GetDetailAsync(long customerNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var cust = await _db.SalCustomers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerNo == customerNo && c.CompanyNo == companyNo && c.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Customer not found: {customerNo}");
        return ToDto(cust);
    }

    public async Task<Sal1101CustomerDto> SaveAsync(Sal1101CustomerDto dto, CancellationToken ct = default)
    {
        return dto.CustomerNo.HasValue && dto.CustomerNo.Value > 0
            ? await UpdateAsync(dto.CustomerNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    private async Task<Sal1101CustomerDto> InsertAsync(Sal1101CustomerDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        Validate(dto);

        string? code = string.IsNullOrWhiteSpace(dto.CustomerCode) ? null : dto.CustomerCode.Trim();

        if (code != null && await ExistsCustomerIdAsync(code, companyNo, null, ct))
            throw new ValidationException($"Customer ID already exists: {code}");

        string? mobile = string.IsNullOrWhiteSpace(dto.Mobile) ? null : dto.Mobile.Trim();
        if (mobile != null && await ExistsMobileAsync(mobile, companyNo, null, ct))
            throw new ValidationException("Mobile number already used by another customer");

        // Gap-free, race-free code from the shared document sequence. The previous
        // CountAsync()+loop was time-of-check/time-of-use: two concurrent inserts could
        // both settle on the same code.
        code ??= await _docSeq.NextAsync(companyNo, null, DocType, "CUS", 6, ct);

        var cust = new SalCustomer
        {
            CompanyNo = companyNo,
            BranchNo = dto.BranchNo ?? _ctx.CurrentBranchNo(),
            CustomerId = code,
            OpeningBalance = dto.OpeningBalance,
            // Java seeds current_due at ZERO — the opening balance reaches the sub-ledger
            // through an opening entry, not by pre-loading the running total. Setting it
            // here would double-count the customer's due.
            CurrentDue = 0m,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };

        Apply(cust, dto);

        _db.SalCustomers.Add(cust);
        await _db.SaveChangesAsync(ct);
        return ToDto(cust);
    }

    private async Task<Sal1101CustomerDto> UpdateAsync(long customerNo, Sal1101CustomerDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        Validate(dto);

        var cust = await _db.SalCustomers
            .FirstOrDefaultAsync(c => c.CustomerNo == customerNo && c.CompanyNo == companyNo && c.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Customer not found: {customerNo}");

        string? code = string.IsNullOrWhiteSpace(dto.CustomerCode) ? null : dto.CustomerCode.Trim();
        if (code != null && code != cust.CustomerId && await ExistsCustomerIdAsync(code, companyNo, customerNo, ct))
            throw new ValidationException($"Customer ID already exists: {code}");

        string? mobile = string.IsNullOrWhiteSpace(dto.Mobile) ? null : dto.Mobile.Trim();
        if (mobile != null && mobile != cust.MobileNo && await ExistsMobileAsync(mobile, companyNo, customerNo, ct))
            throw new ValidationException("Mobile number already used by another customer");

        if (code != null) cust.CustomerId = code;

        // current_due and opening_balance are deliberately NOT updated here — both are
        // derived state owned by the AR sub-ledger, exactly as the Java service documents.
        Apply(cust, dto);

        cust.UpdatedBy = _ctx.CurrentUserNo();
        cust.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(cust);
    }

    public async Task DeleteAsync(long customerNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        var cust = await _db.SalCustomers
            .FirstOrDefaultAsync(c => c.CustomerNo == customerNo && c.CompanyNo == companyNo && c.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Customer not found: {customerNo}");

        // Java blocks the delete while money is outstanding — a customer with a due
        // cannot simply disappear from the AR sub-ledger.
        if (cust.CurrentDue != 0m)
            throw new ValidationException("Cannot delete a customer with an outstanding due — collect it or set the customer inactive");

        if (await _db.SalInvoices.AnyAsync(i => i.CustomerNo == customerNo && i.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete: this customer has invoices — set them inactive instead");

        cust.IsDeleted = 1;
        cust.IsActive = 0;
        cust.DeletedBy = _ctx.CurrentUserNo();
        cust.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Port of Java Sal1101Service.validate. customer_type and price_tier are validated
    /// in Java but neither is carried on the .NET DTO or entity, so those two checks are
    /// intentionally absent — adding them would require the column, the DTO field and the
    /// Angular form together.
    /// </summary>
    private static void Validate(Sal1101CustomerDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerName))
            throw new ValidationException("Customer name is required");
        if (dto.CreditLimit < 0)
            throw new ValidationException("Credit limit cannot be negative");
        if (dto.CreditDays < 0)
            throw new ValidationException("Credit days cannot be negative");
    }

    private Task<bool> ExistsCustomerIdAsync(string code, long companyNo, long? excludeNo, CancellationToken ct) =>
        _db.SalCustomers.AnyAsync(c => c.CustomerId == code && c.CompanyNo == companyNo && c.IsDeleted == 0
                                       && (excludeNo == null || c.CustomerNo != excludeNo), ct);

    private Task<bool> ExistsMobileAsync(string mobile, long companyNo, long? excludeNo, CancellationToken ct) =>
        _db.SalCustomers.AnyAsync(c => c.MobileNo == mobile && c.CompanyNo == companyNo && c.IsDeleted == 0
                                       && (excludeNo == null || c.CustomerNo != excludeNo), ct);

    /// <summary>Copies the editable fields. Never touches current_due or the business key.</summary>
    private static void Apply(SalCustomer c, Sal1101CustomerDto dto)
    {
        if (dto.CustomerName != null) c.CustomerName = dto.CustomerName.Trim();
        c.Email = dto.Email;
        c.MobileNo = string.IsNullOrWhiteSpace(dto.Mobile) ? null : dto.Mobile.Trim();
        c.AddressLine1 = dto.Address;
        c.City = dto.City;
        c.CountryCode = dto.Country;
        c.VatRegNo = dto.TaxNumber;
        c.IsCreditAllowed = dto.IsCreditAllowed;
        c.CreditLimit = dto.CreditLimit;
        c.CreditDays = dto.CreditDays;
        c.IsActive = dto.IsActive;
    }

    private static Sal1101CustomerDto ToDto(SalCustomer c) => new()
    {
        CustomerNo = c.CustomerNo,
        CustomerCode = c.CustomerId,
        CustomerName = c.CustomerName,
        IsCreditAllowed = c.IsCreditAllowed,
        Email = c.Email,
        Phone = c.Phone,
        Mobile = c.Mobile,
        Address = c.AddressLine1,
        City = c.City,
        Country = c.CountryCode,
        TaxNumber = c.VatRegNo,
        CreditLimit = c.CreditLimit,
        CreditDays = c.CreditDays,
        OpeningBalance = c.OpeningBalance,
        CurrentBalance = c.CurrentDue,
        BranchNo = c.BranchNo,
        IsActive = c.IsActive ?? 0,
        RowVersion = c.RowVersion
    };
}
