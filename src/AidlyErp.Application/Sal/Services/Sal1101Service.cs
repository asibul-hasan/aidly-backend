using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Sal.Dto;
using AidlyErp.Domain.Sal;

namespace AidlyErp.Application.Sal.Services;

public interface ISal1101Service
{
    Task<List<Sal1101CustomerDto>> GetListAsync(CancellationToken ct = default);
    Task<Sal1101CustomerDto> GetDetailAsync(long customerNo, CancellationToken ct = default);
    Task<Sal1101CustomerDto> SaveAsync(Sal1101CustomerDto dto, CancellationToken ct = default);
    Task DeleteAsync(long customerNo, CancellationToken ct = default);
}

public class Sal1101Service : ISal1101Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Sal1101Service(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Sal1101CustomerDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.SalCustomers
            .AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.IsDeleted == 0)
            .OrderBy(c => c.CustomerName)
            .Select(c => ToDto(c))
            .ToListAsync(ct);
    }

    public async Task<Sal1101CustomerDto> GetDetailAsync(long customerNo, CancellationToken ct = default)
    {
        var cust = await _db.SalCustomers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerNo == customerNo && c.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Customer not found: {customerNo}");
        return ToDto(cust);
    }

    public async Task<Sal1101CustomerDto> SaveAsync(Sal1101CustomerDto dto, CancellationToken ct = default)
    {
        if (dto.CustomerNo.HasValue && dto.CustomerNo.Value > 0)
        {
            var cust = await _db.SalCustomers.FirstOrDefaultAsync(c => c.CustomerNo == dto.CustomerNo.Value && c.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Customer not found: {dto.CustomerNo}");

            if (dto.CustomerName != null) cust.CustomerName = dto.CustomerName.Trim();
            cust.CompanyName = dto.CompanyName;
            cust.CustomerGroup = dto.CustomerGroup;
            cust.Email = dto.Email;
            cust.Phone = dto.Phone;
            cust.Mobile = dto.Mobile;
            cust.Address = dto.Address;
            cust.City = dto.City;
            cust.Country = dto.Country;
            cust.TaxNumber = dto.TaxNumber;
            cust.CreditLimit = dto.CreditLimit;
            cust.CreditDays = dto.CreditDays;
            cust.GlAccountNo = dto.GlAccountNo;
            cust.IsActive = dto.IsActive;
            cust.UpdatedBy = _ctx.CurrentUserNo(); cust.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return ToDto(cust);
        }
        else
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            if (string.IsNullOrWhiteSpace(dto.CustomerName)) throw new ValidationException("Customer name is required");

            string code = string.IsNullOrWhiteSpace(dto.CustomerCode) ? await NextCustomerCodeAsync(companyNo, ct) : dto.CustomerCode.Trim().ToUpperInvariant();

            var cust = new SalCustomer
            {
                CompanyNo = companyNo,
                BranchNo = dto.BranchNo ?? _ctx.CurrentBranchNo(),
                CustomerCode = code,
                CustomerName = dto.CustomerName.Trim(),
                CompanyName = dto.CompanyName,
                CustomerGroup = dto.CustomerGroup,
                Email = dto.Email,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                Address = dto.Address,
                City = dto.City,
                Country = dto.Country,
                TaxNumber = dto.TaxNumber,
                CreditLimit = dto.CreditLimit,
                CreditDays = dto.CreditDays,
                OpeningBalance = dto.OpeningBalance,
                CurrentBalance = dto.OpeningBalance,
                GlAccountNo = dto.GlAccountNo,
                IsActive = dto.IsActive,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };

            _db.SalCustomers.Add(cust);
            await _db.SaveChangesAsync(ct);
            return ToDto(cust);
        }
    }

    public async Task DeleteAsync(long customerNo, CancellationToken ct = default)
    {
        var cust = await _db.SalCustomers.FirstOrDefaultAsync(c => c.CustomerNo == customerNo && c.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Customer not found: {customerNo}");

        if (await _db.SalInvoices.AnyAsync(i => i.CustomerNo == customerNo && i.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete customer with existing invoices");

        cust.IsDeleted = 1; cust.IsActive = 0;
        cust.DeletedBy = _ctx.CurrentUserNo(); cust.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> NextCustomerCodeAsync(long companyNo, CancellationToken ct)
    {
        long count = await _db.SalCustomers.CountAsync(c => c.CompanyNo == companyNo, ct) + 1;
        string code;
        do { code = $"CUST{count++:D4}"; }
        while (await _db.SalCustomers.AnyAsync(c => c.CustomerCode == code && c.CompanyNo == companyNo && c.IsDeleted == 0, ct));
        return code;
    }

    private static Sal1101CustomerDto ToDto(SalCustomer c) => new()
    {
        CustomerNo = c.CustomerNo,
        CustomerCode = c.CustomerCode,
        CustomerName = c.CustomerName,
        CompanyName = c.CompanyName,
        CustomerGroup = c.CustomerGroup,
        Email = c.Email,
        Phone = c.Phone,
        Mobile = c.Mobile,
        Address = c.Address,
        City = c.City,
        Country = c.Country,
        TaxNumber = c.TaxNumber,
        CreditLimit = c.CreditLimit,
        CreditDays = c.CreditDays,
        OpeningBalance = c.OpeningBalance,
        CurrentBalance = c.CurrentBalance,
        GlAccountNo = c.GlAccountNo,
        BranchNo = c.BranchNo,
        IsActive = c.IsActive,
        RowVersion = c.RowVersion
    };
}
