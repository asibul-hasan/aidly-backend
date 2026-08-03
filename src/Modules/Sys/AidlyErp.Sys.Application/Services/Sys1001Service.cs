using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Sys.Application.Services;

public interface ISys1001Service
{
    Task<Sys1001CompanyDto> GetDetailAsync(long companyNo, CancellationToken cancellationToken = default);

    Task<Sys1001CompanyDto> InsertAsync(Sys1001CompanyDto dto, CancellationToken cancellationToken = default);

    Task<Sys1001CompanyDto> UpdateAsync(long companyNo, Sys1001CompanyDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(long companyNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dedicated service for the SYS1001 Company Setup form.
///
/// <para>Logo management is handled by the reusable core file API
/// (<c>/api/v1/sys/files</c>). After a logo is uploaded or removed via that API the frontend
/// passes the updated <c>logo_path</c> (or <c>null</c>) in the regular save payload so it is
/// persisted to <c>company.logo_path</c>.</para>
/// </summary>
public class Sys1001Service : ISys1001Service
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1001Service> _logger;

    public Sys1001Service(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1001Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    // ─── Reads ───────────────────────────────────────────────────────────────

    public async Task<Sys1001CompanyDto> GetDetailAsync(long companyNo, CancellationToken cancellationToken = default) =>
        ToDto(await LoadLiveAsync(companyNo, cancellationToken));

    // ─── Writes ──────────────────────────────────────────────────────────────

    public Task<Sys1001CompanyDto> InsertAsync(Sys1001CompanyDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            if (string.IsNullOrWhiteSpace(dto.CompanyId))
            {
                throw new ValidationException("Company ID is required");
            }

            var duplicate = await _db.Companies
                .AnyAsync(c => c.CompanyId == dto.CompanyId && c.IsDeleted == Deleted, ct);

            if (duplicate)
            {
                throw new ValidationException("Company ID already exists: " + dto.CompanyId);
            }

            var entity = new Company();
            ApplyDtoToEntity(dto, entity);

            _db.Companies.Add(entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Company inserted: companyNo={CompanyNo}, companyId={CompanyId}",
                entity.CompanyNo, entity.CompanyId);

            return ToDto(entity);
        }, cancellationToken);

    public Task<Sys1001CompanyDto> UpdateAsync(long companyNo, Sys1001CompanyDto dto,
                                               CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadLiveAsync(companyNo, ct);

            if (dto.CompanyId != null)
            {
                var existing = await _db.Companies
                    .FirstOrDefaultAsync(c => c.CompanyId == dto.CompanyId && c.IsDeleted == Deleted, ct);

                if (existing != null && existing.CompanyNo != companyNo)
                {
                    throw new ValidationException("Company ID already in use: " + dto.CompanyId);
                }
            }

            ApplyDtoToEntity(dto, entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Company updated: companyNo={CompanyNo}", entity.CompanyNo);

            return ToDto(entity);
        }, cancellationToken);

    public Task DeleteAsync(long companyNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadLiveAsync(companyNo, ct);

            entity.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Company soft-deleted: companyNo={CompanyNo}", companyNo);
        }, cancellationToken);

    // ─── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Loads the company, but only if it is the caller's own tenant — a user may never read or
    /// write another company's row through this form.
    /// </summary>
    private async Task<Company> LoadLiveAsync(long companyNo, CancellationToken cancellationToken)
    {
        var authCompanyNo = _ctx.CompanyNo;
        if (companyNo != authCompanyNo)
        {
            throw new NotFoundException("Company not found: companyNo=" + companyNo);
        }

        return await _db.Companies.FirstOrDefaultAsync(c => c.CompanyNo == companyNo && c.IsDeleted == Deleted,
                                                       cancellationToken)
               ?? throw new NotFoundException("Company not found: companyNo=" + companyNo);
    }

    private static Sys1001CompanyDto ToDto(Company e) => new()
    {
        CompanyNo = e.CompanyNo,
        CompanyId = e.CompanyId,
        CompanyName = e.CompanyName,
        CompanyNameNls = e.CompanyNameNls,
        CompanyType = e.CompanyType,
        TradeLicenseNo = e.TradeLicenseNo,
        VatRegNo = e.VatRegNo,
        TinNo = e.TinNo,
        BinNo = e.BinNo,
        RegNo = e.RegNo,
        CompanyAddr1 = e.CompanyAddr1,
        CompanyAddr2 = e.CompanyAddr2,
        City = e.City,
        StateProvince = e.StateProvince,
        PostCode = e.PostCode,
        CountryCode = e.CountryCode,
        MobileNo = e.MobileNo,
        ContactNo = e.ContactNo,
        Email = e.Email,
        Website = e.Website,
        LogoPath = e.LogoPath,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };

    private static void ApplyDtoToEntity(Sys1001CompanyDto dto, Company e)
    {
        if (dto.CompanyId != null) e.CompanyId = dto.CompanyId;
        if (dto.CompanyName != null) e.CompanyName = dto.CompanyName;
        if (dto.CompanyNameNls != null) e.CompanyNameNls = dto.CompanyNameNls;
        if (dto.CompanyType != null) e.CompanyType = dto.CompanyType;
        if (dto.TradeLicenseNo != null) e.TradeLicenseNo = dto.TradeLicenseNo;
        if (dto.VatRegNo != null) e.VatRegNo = dto.VatRegNo;
        if (dto.TinNo != null) e.TinNo = dto.TinNo;
        if (dto.BinNo != null) e.BinNo = dto.BinNo;
        if (dto.RegNo != null) e.RegNo = dto.RegNo;
        if (dto.CompanyAddr1 != null) e.CompanyAddr1 = dto.CompanyAddr1;
        if (dto.CompanyAddr2 != null) e.CompanyAddr2 = dto.CompanyAddr2;
        if (dto.City != null) e.City = dto.City;
        if (dto.StateProvince != null) e.StateProvince = dto.StateProvince;
        if (dto.PostCode != null) e.PostCode = dto.PostCode;
        if (dto.CountryCode != null) e.CountryCode = dto.CountryCode;
        if (dto.MobileNo != null) e.MobileNo = dto.MobileNo;
        if (dto.ContactNo != null) e.ContactNo = dto.ContactNo;
        if (dto.Email != null) e.Email = dto.Email;
        if (dto.Website != null) e.Website = dto.Website;

        // logo_path: always set (null clears it when the logo is removed)
        e.LogoPath = dto.LogoPath;

        if (dto.IsActive != null) e.IsActive = dto.IsActive.Value;
    }
}
