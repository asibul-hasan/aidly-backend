using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Core.Utils;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Sys.Application.Services;

public interface IBranchService
{
    Task<BranchDto> InsertAsync(BranchDto dto, CancellationToken cancellationToken = default);
    Task<BranchDto> UpdateAsync(long branchNo, BranchDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(long branchNo, CancellationToken cancellationToken = default);

    /// <summary>Only the branches the caller is a member of.</summary>
    Task<List<BranchDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<List<BranchDto>> GetListByCompanyAsync(long companyNo, CancellationToken cancellationToken = default);
    Task<BranchDto> GetDtlAsync(long branchNo, CancellationToken cancellationToken = default);
}

/// <summary>Generic branch CRUD behind <c>/api/v1/sys/branches</c>.</summary>
public class BranchService : IBranchService
{
    private const short Active = 1;
    private const short Deleted = 0;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<BranchService> _logger;

    public BranchService(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                         ILogger<BranchService> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    public Task<BranchDto> InsertAsync(BranchDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var companyNo = dto.CompanyNo ?? throw new ValidationException("Company is required");

            var companyExists = await _db.Companies.IgnoreQueryFilters()
                .AnyAsync(c => c.CompanyNo == companyNo && c.IsDeleted == Deleted, ct);

            if (!companyExists)
            {
                throw new NotFoundException("Company not found or inactive: companyNo=" + companyNo);
            }

            if (string.IsNullOrWhiteSpace(dto.BranchId))
            {
                dto.BranchId = await GenerateBranchIdAsync(companyNo, dto.BranchName, dto.City, ct);
            }
            else if (await BranchIdExistsAsync(companyNo, dto.BranchId, ct))
            {
                throw new ValidationException("Branch ID already exists in this company: " + dto.BranchId);
            }

            // At most one main branch per company.
            if (IsMain(dto.IsMainBranch))
            {
                var existingMain = await _db.Branches.AsNoTracking().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(b => b.CompanyNo == companyNo && b.IsMainBranch == Active
                                              && b.IsDeleted == Deleted, ct);

                if (existingMain != null)
                {
                    throw new ValidationException(
                        "A main branch already exists for this company: " + existingMain.BranchName);
                }
            }

            var entity = new Branch { CompanyNo = companyNo };
            Apply(dto, entity);

            _db.Branches.Add(entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Branch inserted: branchNo={BranchNo}, branchId={BranchId}",
                entity.BranchNo, entity.BranchId);

            return ToDto(entity);
        }, cancellationToken);

    /// <summary>
    /// Builds <c>FIRSTWORD-CITY-nnn</c> from the branch name and city (city defaults to
    /// <c>HQ</c>), incrementing the sequence until it is unique within the company. Capped at 15
    /// characters, matching the column width.
    /// </summary>
    private async Task<string> GenerateBranchIdAsync(long companyNo, string? branchName, string? city,
                                                     CancellationToken ct)
    {
        var firstWord = FirstToken(branchName);
        var cityPart = string.IsNullOrWhiteSpace(city) ? "HQ" : FirstToken(city);

        var prefix = firstWord + "-" + cityPart + "-";
        var seq = 1;
        string candidate;

        do
        {
            candidate = prefix + seq++.ToString("D3");
            if (candidate.Length > 15) candidate = candidate[..15];
        }
        while (await BranchIdExistsAsync(companyNo, candidate, ct));

        return candidate;
    }

    private static string FirstToken(string? value)
    {
        var token = (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                    ?? string.Empty;

        return new string(token.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private Task<bool> BranchIdExistsAsync(long companyNo, string branchId, CancellationToken ct) =>
        _db.Branches.IgnoreQueryFilters()
            .AnyAsync(b => b.CompanyNo == companyNo && b.BranchId == branchId && b.IsDeleted == Deleted, ct);

    public Task<BranchDto> UpdateAsync(long branchNo, BranchDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(branchNo, ct);

            if (IsMain(entity.IsMainBranch) && dto.IsActive == 0)
            {
                throw new ValidationException("Main branch cannot be deactivated.");
            }

            // is_main_branch is not editable here — preserve whatever the row already has.
            dto.IsMainBranch = entity.IsMainBranch;

            Apply(dto, entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Branch updated: branchNo={BranchNo}", entity.BranchNo);
            return ToDto(entity);
        }, cancellationToken);

    public Task DeleteAsync(long branchNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(branchNo, ct);

            if (IsMain(entity.IsMainBranch))
            {
                throw new ValidationException("Main branch cannot be deleted.");
            }

            entity.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Branch soft-deleted: branchNo={BranchNo}", branchNo);
        }, cancellationToken);

    /// <summary>
    /// Scoped to the caller's own memberships — an unauthenticated request, or one with no
    /// branch assignments, gets an empty list rather than every branch.
    /// </summary>
    public async Task<List<BranchDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var userNo = _ctx.UserNo;
        var companyNo = _ctx.CompanyNo;
        if (userNo == null || companyNo == null) return new List<BranchDto>();

        var branchNos = await _db.UserBranches
            .AsNoTracking()
            .Where(ub => ub.UserNo == userNo && ub.IsActive == Active && ub.IsDeleted == Deleted)
            .Select(ub => ub.BranchNo)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (branchNos.Count == 0) return new List<BranchDto>();

        return (await _db.Branches.AsNoTracking().IgnoreQueryFilters()
                .Where(b => b.CompanyNo == companyNo && branchNos.Contains(b.BranchNo) && b.IsDeleted == Deleted)
                .ToListAsync(cancellationToken))
            .Select(ToDto).ToList();
    }

    public async Task<List<BranchDto>> GetListByCompanyAsync(long companyNo,
                                                             CancellationToken cancellationToken = default) =>
        (await _db.Branches.AsNoTracking().IgnoreQueryFilters()
            .Where(b => b.CompanyNo == companyNo && b.IsDeleted == Deleted)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<BranchDto> GetDtlAsync(long branchNo, CancellationToken cancellationToken = default) =>
        ToDto(await LoadAsync(branchNo, cancellationToken));

    private async Task<Branch> LoadAsync(long branchNo, CancellationToken ct) =>
        await _db.Branches.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.BranchNo == branchNo && b.IsDeleted == Deleted, ct)
        ?? throw new NotFoundException("Branch not found: branchNo=" + branchNo);

    private static bool IsMain(short? flag) => flag == 1;

    private static void Apply(BranchDto dto, Branch e)
    {
        if (dto.CompanyNo != null) e.CompanyNo = dto.CompanyNo.Value;
        if (dto.BranchId != null) e.BranchId = dto.BranchId;
        if (dto.BranchName != null) e.BranchName = dto.BranchName;
        if (dto.BranchNameNls != null) e.BranchNameNls = dto.BranchNameNls;
        if (dto.BranchType != null) e.BranchType = dto.BranchType;
        if (dto.BranchAddr1 != null) e.BranchAddr1 = dto.BranchAddr1;
        if (dto.BranchAddr2 != null) e.BranchAddr2 = dto.BranchAddr2;
        if (dto.City != null) e.City = dto.City;
        if (dto.PostCode != null) e.PostCode = dto.PostCode;
        if (dto.MobileNo != null) e.MobileNo = dto.MobileNo;
        if (dto.ContactNo != null) e.ContactNo = dto.ContactNo;
        if (dto.Email != null) e.Email = dto.Email;
        if (dto.ManagerEmployeeNo != null) e.ManagerEmployeeNo = dto.ManagerEmployeeNo;
        if (dto.IsMainBranch != null) e.IsMainBranch = dto.IsMainBranch.Value;
        if (dto.IsActive != null) e.IsActive = dto.IsActive.Value;
    }

    private static BranchDto ToDto(Branch e) => new()
    {
        BranchNo = e.BranchNo,
        BranchId = e.BranchId,
        BranchName = e.BranchName,
        BranchNameNls = e.BranchNameNls,
        BranchType = e.BranchType,
        BranchAddr1 = e.BranchAddr1,
        BranchAddr2 = e.BranchAddr2,
        City = e.City,
        PostCode = e.PostCode,
        MobileNo = e.MobileNo,
        ContactNo = e.ContactNo,
        Email = e.Email,
        ManagerEmployeeNo = e.ManagerEmployeeNo,
        CompanyNo = e.CompanyNo,
        IsMainBranch = e.IsMainBranch,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
