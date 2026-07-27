using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Common.Utils;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Domain.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Application.Sys.Services;

public interface ICompanyService
{
    Task<CompanyDto> InsertAsync(CompanyDto dto, CancellationToken cancellationToken = default);
    Task<CompanyDto> UpdateAsync(long companyNo, CompanyDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(long companyNo, CancellationToken cancellationToken = default);
    Task<List<CompanyDto>> GetListAsync(CancellationToken cancellationToken = default);
    Task<CompanyDto> GetDtlAsync(long companyNo, CancellationToken cancellationToken = default);
}

/// <summary>Generic company CRUD behind <c>/api/v1/sys/companies</c>.</summary>
public class CompanyService : ICompanyService
{
    private const short Deleted = 0;

    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly FileStorageUtil _fileStorage;
    private readonly ILogger<CompanyService> _logger;

    public CompanyService(IApplicationDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                          FileStorageUtil fileStorage, ILogger<CompanyService> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public Task<CompanyDto> InsertAsync(CompanyDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var companyId = dto.CompanyId ?? string.Empty;

            if (await _db.Companies.IgnoreQueryFilters()
                    .AnyAsync(c => c.CompanyId == companyId && c.IsDeleted == Deleted, ct))
            {
                throw new ValidationException("Company ID already exists: " + companyId);
            }

            // A data-image payload is written to disk and replaced by its stored path.
            if (dto.LogoPath != null)
            {
                dto.LogoPath = _fileStorage.SaveBase64Image(dto.LogoPath, "company", "logo");
            }

            var entity = new Company();
            Apply(dto, entity);

            _db.Companies.Add(entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Company inserted: companyNo={CompanyNo}, companyId={CompanyId}",
                entity.CompanyNo, entity.CompanyId);

            return ToDto(entity);
        }, cancellationToken);

    public Task<CompanyDto> UpdateAsync(long companyNo, CompanyDto dto,
                                        CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(companyNo, ct);

            if (dto.CompanyId != null)
            {
                var clash = await _db.Companies.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.CompanyId == dto.CompanyId && c.IsDeleted == Deleted, ct);

                if (clash != null && clash.CompanyNo != companyNo)
                {
                    throw new ValidationException("Company ID already in use: " + dto.CompanyId);
                }
            }

            if (dto.LogoPath != null)
            {
                dto.LogoPath = _fileStorage.SaveBase64Image(dto.LogoPath, "company", "logo");
            }

            Apply(dto, entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Company updated: companyNo={CompanyNo}", entity.CompanyNo);
            return ToDto(entity);
        }, cancellationToken);

    public Task DeleteAsync(long companyNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(companyNo, ct);
            entity.PerformSoftDelete(_ctx.CurrentUserNo());

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Company soft-deleted: companyNo={CompanyNo}", companyNo);
        }, cancellationToken);

    public async Task<List<CompanyDto>> GetListAsync(CancellationToken cancellationToken = default) =>
        (await _db.Companies.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.IsDeleted == Deleted)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<CompanyDto> GetDtlAsync(long companyNo, CancellationToken cancellationToken = default) =>
        ToDto(await LoadAsync(companyNo, cancellationToken));

    private async Task<Company> LoadAsync(long companyNo, CancellationToken ct) =>
        await _db.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.CompanyNo == companyNo && c.IsDeleted == Deleted, ct)
        ?? throw new NotFoundException("Company not found: companyNo=" + companyNo);

    private static void Apply(CompanyDto dto, Company e)
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
        if (dto.LogoPath != null) e.LogoPath = dto.LogoPath;
        if (dto.IsActive != null) e.IsActive = dto.IsActive.Value;
    }

    private static CompanyDto ToDto(Company e) => new()
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
}
