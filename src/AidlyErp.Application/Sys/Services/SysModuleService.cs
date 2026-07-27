using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Domain.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Application.Sys.Services;

public interface ISysModuleService
{
    Task<SysModuleDto> InsertAsync(SysModuleDto dto, CancellationToken cancellationToken = default);
    Task<SysModuleDto> UpdateAsync(long moduleNo, SysModuleDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(long moduleNo, CancellationToken cancellationToken = default);
    Task<List<SysModuleDto>> GetListAsync(CancellationToken cancellationToken = default);
    Task<SysModuleDto> GetDtlAsync(long moduleNo, CancellationToken cancellationToken = default);
}

public class SysModuleService : ISysModuleService
{
    private const short Deleted = 0;

    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<SysModuleService> _logger;

    public SysModuleService(IApplicationDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                            ILogger<SysModuleService> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    public Task<SysModuleDto> InsertAsync(SysModuleDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var code = dto.ModuleCode ?? string.Empty;

            if (await _db.SysModules.AnyAsync(m => m.ModuleCode == code && m.IsDeleted == Deleted, ct))
            {
                throw new ValidationException("Module code already exists: " + code);
            }

            var entity = new SysModule();
            Apply(dto, entity);

            _db.SysModules.Add(entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("SysModule inserted: moduleNo={ModuleNo}", entity.ModuleNo);
            return ToDto(entity);
        }, cancellationToken);

    public Task<SysModuleDto> UpdateAsync(long moduleNo, SysModuleDto dto,
                                          CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(moduleNo, ct);
            Apply(dto, entity);

            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }, cancellationToken);

    public Task DeleteAsync(long moduleNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(moduleNo, ct);
            entity.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    public async Task<List<SysModuleDto>> GetListAsync(CancellationToken cancellationToken = default) =>
        (await _db.SysModules.AsNoTracking().Where(m => m.IsDeleted == Deleted).ToListAsync(cancellationToken))
        .Select(ToDto).ToList();

    public async Task<SysModuleDto> GetDtlAsync(long moduleNo, CancellationToken cancellationToken = default) =>
        ToDto(await LoadAsync(moduleNo, cancellationToken));

    private async Task<SysModule> LoadAsync(long moduleNo, CancellationToken ct) =>
        await _db.SysModules.FirstOrDefaultAsync(m => m.ModuleNo == moduleNo && m.IsDeleted == Deleted, ct)
        ?? throw new NotFoundException("Module not found: moduleNo=" + moduleNo);

    /// <summary>Null-skipping patch, mirroring the MapStruct mapper's NullValuePropertyMappingStrategy.</summary>
    private static void Apply(SysModuleDto dto, SysModule e)
    {
        if (dto.ModuleCode != null) e.ModuleCode = dto.ModuleCode;
        if (dto.ModuleName != null) e.ModuleName = dto.ModuleName;
        if (dto.ModuleDesc != null) e.ModuleDesc = dto.ModuleDesc;
        if (dto.ModuleIcon != null) e.ModuleIcon = dto.ModuleIcon;
        if (dto.ModuleRoute != null) e.ModuleRoute = dto.ModuleRoute;
        if (dto.OrderSl != null) e.OrderSl = dto.OrderSl;
        if (dto.IsActive != null) e.IsActive = dto.IsActive.Value;
    }

    private static SysModuleDto ToDto(SysModule e) => new()
    {
        ModuleNo = e.ModuleNo,
        ModuleCode = e.ModuleCode,
        ModuleName = e.ModuleName,
        ModuleDesc = e.ModuleDesc,
        ModuleIcon = e.ModuleIcon,
        ModuleRoute = e.ModuleRoute,
        OrderSl = e.OrderSl,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
