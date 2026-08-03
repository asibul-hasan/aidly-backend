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

public interface ISysSubmoduleService
{
    Task<SysSubmoduleDto> InsertAsync(SysSubmoduleDto dto, CancellationToken cancellationToken = default);
    Task<SysSubmoduleDto> UpdateAsync(long submoduleNo, SysSubmoduleDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(long submoduleNo, CancellationToken cancellationToken = default);
    Task<List<SysSubmoduleDto>> GetListAsync(CancellationToken cancellationToken = default);
    Task<List<SysSubmoduleDto>> GetListByModuleAsync(long moduleNo, CancellationToken cancellationToken = default);
    Task<SysSubmoduleDto> GetDtlAsync(long submoduleNo, CancellationToken cancellationToken = default);
}

public class SysSubmoduleService : ISysSubmoduleService
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<SysSubmoduleService> _logger;

    public SysSubmoduleService(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                               ILogger<SysSubmoduleService> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    public Task<SysSubmoduleDto> InsertAsync(SysSubmoduleDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            if (dto.ModuleNo == null) throw new ValidationException("moduleNo is required");

            var moduleExists = await _db.SysModules
                .AnyAsync(m => m.ModuleNo == dto.ModuleNo && m.IsDeleted == Deleted, ct);

            if (!moduleExists)
            {
                throw new NotFoundException("Module not found: moduleNo=" + dto.ModuleNo);
            }

            var code = dto.SubmoduleCode ?? string.Empty;

            if (await _db.SysSubmodules.AnyAsync(s => s.ModuleNo == dto.ModuleNo && s.SubmoduleCode == code
                                                      && s.IsDeleted == Deleted, ct))
            {
                throw new ValidationException("Submodule code already exists in this module: " + code);
            }

            var entity = new SysSubmodule { ModuleNo = dto.ModuleNo.Value };
            Apply(dto, entity);

            _db.SysSubmodules.Add(entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("SysSubmodule inserted: submoduleNo={SubmoduleNo}", entity.SubmoduleNo);
            return ToDto(entity);
        }, cancellationToken);

    public Task<SysSubmoduleDto> UpdateAsync(long submoduleNo, SysSubmoduleDto dto,
                                             CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(submoduleNo, ct);
            Apply(dto, entity);

            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }, cancellationToken);

    public Task DeleteAsync(long submoduleNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(submoduleNo, ct);
            entity.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    public async Task<List<SysSubmoduleDto>> GetListAsync(CancellationToken cancellationToken = default) =>
        (await _db.SysSubmodules.AsNoTracking().Where(s => s.IsDeleted == Deleted).ToListAsync(cancellationToken))
        .Select(ToDto).ToList();

    public async Task<List<SysSubmoduleDto>> GetListByModuleAsync(long moduleNo,
                                                                  CancellationToken cancellationToken = default) =>
        (await _db.SysSubmodules.AsNoTracking()
            .Where(s => s.ModuleNo == moduleNo && s.IsDeleted == Deleted)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<SysSubmoduleDto> GetDtlAsync(long submoduleNo, CancellationToken cancellationToken = default) =>
        ToDto(await LoadAsync(submoduleNo, cancellationToken));

    private async Task<SysSubmodule> LoadAsync(long submoduleNo, CancellationToken ct) =>
        await _db.SysSubmodules.FirstOrDefaultAsync(s => s.SubmoduleNo == submoduleNo && s.IsDeleted == Deleted, ct)
        ?? throw new NotFoundException("Submodule not found: submoduleNo=" + submoduleNo);

    private static void Apply(SysSubmoduleDto dto, SysSubmodule e)
    {
        if (dto.ModuleNo != null) e.ModuleNo = dto.ModuleNo.Value;
        if (dto.SubmoduleCode != null) e.SubmoduleCode = dto.SubmoduleCode;
        if (dto.SubmoduleName != null) e.SubmoduleName = dto.SubmoduleName;
        if (dto.SubmoduleIcon != null) e.SubmoduleIcon = dto.SubmoduleIcon;
        if (dto.SubmoduleRoute != null) e.SubmoduleRoute = dto.SubmoduleRoute;
        if (dto.OrderSl != null) e.OrderSl = dto.OrderSl;
        if (dto.IsActive != null) e.IsActive = dto.IsActive.Value;
    }

    private static SysSubmoduleDto ToDto(SysSubmodule e) => new()
    {
        SubmoduleNo = e.SubmoduleNo,
        ModuleNo = e.ModuleNo,
        SubmoduleCode = e.SubmoduleCode,
        SubmoduleName = e.SubmoduleName,
        SubmoduleIcon = e.SubmoduleIcon,
        SubmoduleRoute = e.SubmoduleRoute,
        OrderSl = e.OrderSl,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
