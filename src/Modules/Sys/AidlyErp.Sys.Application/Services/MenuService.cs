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

public interface IMenuService
{
    Task<MenuDto> InsertAsync(MenuDto dto, CancellationToken cancellationToken = default);
    Task<MenuDto> UpdateAsync(long menuNo, MenuDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(long menuNo, CancellationToken cancellationToken = default);
    Task<List<MenuDto>> GetListAsync(CancellationToken cancellationToken = default);
    Task<List<MenuDto>> GetListBySubmoduleAsync(long submoduleNo, CancellationToken cancellationToken = default);

    /// <summary>Menus reached via the module's submodules. Marked deprecated in the Java source.</summary>
    [Obsolete("Menus are owned by a submodule; prefer GetListBySubmoduleAsync.")]
    Task<List<MenuDto>> GetListByModuleAsync(long moduleNo, CancellationToken cancellationToken = default);

    Task<MenuDto> GetDtlAsync(long menuNo, CancellationToken cancellationToken = default);
}

public class MenuService : IMenuService
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly IPathFormCacheInvalidator _pathFormCache;
    private readonly ILogger<MenuService> _logger;

    public MenuService(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                       IPathFormCacheInvalidator pathFormCache, ILogger<MenuService> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _pathFormCache = pathFormCache;
        _logger = logger;
    }

    public Task<MenuDto> InsertAsync(MenuDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            if (string.IsNullOrWhiteSpace(dto.FormId)) throw new ValidationException("formId is required");

            if (await _db.Menus.AnyAsync(m => m.FormId == dto.FormId && m.IsDeleted == Deleted, ct))
            {
                throw new ValidationException("Form ID already exists: " + dto.FormId);
            }

            if (dto.SubmoduleNo == null)
            {
                throw new ValidationException("submoduleNo is required (menus are owned by a submodule)");
            }

            var submoduleExists = await _db.SysSubmodules
                .AnyAsync(s => s.SubmoduleNo == dto.SubmoduleNo && s.IsDeleted == Deleted, ct);

            if (!submoduleExists)
            {
                throw new NotFoundException("Submodule not found: submoduleNo=" + dto.SubmoduleNo);
            }

            var entity = new Menu { SubmoduleNo = dto.SubmoduleNo.Value };
            Apply(dto, entity);

            _db.Menus.Add(entity);
            await _db.SaveChangesAsync(ct);

            // A new route means the memoized URL→form map is stale.
            _pathFormCache.EvictPathFormCache();

            _logger.LogInformation("Menu inserted: menuNo={MenuNo}, formId={FormId}", entity.MenuNo, entity.FormId);
            return ToDto(entity);
        }, cancellationToken);

    public Task<MenuDto> UpdateAsync(long menuNo, MenuDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(menuNo, ct);
            Apply(dto, entity);

            await _db.SaveChangesAsync(ct);
            _pathFormCache.EvictPathFormCache();

            _logger.LogInformation("Menu updated: menuNo={MenuNo}", entity.MenuNo);
            return ToDto(entity);
        }, cancellationToken);

    public Task DeleteAsync(long menuNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(menuNo, ct);
            entity.PerformSoftDelete(_ctx.CurrentUserNo());

            await _db.SaveChangesAsync(ct);
            _pathFormCache.EvictPathFormCache();

            _logger.LogInformation("Menu soft-deleted: menuNo={MenuNo}", menuNo);
        }, cancellationToken);

    public async Task<List<MenuDto>> GetListAsync(CancellationToken cancellationToken = default) =>
        (await _db.Menus.AsNoTracking().Where(m => m.IsDeleted == Deleted).ToListAsync(cancellationToken))
        .Select(ToDto).ToList();

    public async Task<List<MenuDto>> GetListBySubmoduleAsync(long submoduleNo,
                                                             CancellationToken cancellationToken = default) =>
        (await _db.Menus.AsNoTracking()
            .Where(m => m.SubmoduleNo == submoduleNo && m.IsDeleted == Deleted)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList();

    [Obsolete("Menus are owned by a submodule; prefer GetListBySubmoduleAsync.")]
    public async Task<List<MenuDto>> GetListByModuleAsync(long moduleNo,
                                                          CancellationToken cancellationToken = default)
    {
        var submoduleNos = await _db.SysSubmodules
            .AsNoTracking()
            .Where(s => s.ModuleNo == moduleNo && s.IsDeleted == Deleted)
            .Select(s => s.SubmoduleNo)
            .ToListAsync(cancellationToken);

        return (await _db.Menus
                .AsNoTracking()
                .Where(m => submoduleNos.Contains(m.SubmoduleNo) && m.IsDeleted == Deleted)
                .ToListAsync(cancellationToken))
            .Select(ToDto).ToList();
    }

    public async Task<MenuDto> GetDtlAsync(long menuNo, CancellationToken cancellationToken = default) =>
        ToDto(await LoadAsync(menuNo, cancellationToken));

    private async Task<Menu> LoadAsync(long menuNo, CancellationToken ct) =>
        await _db.Menus.FirstOrDefaultAsync(m => m.MenuNo == menuNo && m.IsDeleted == Deleted, ct)
        ?? throw new NotFoundException("Menu not found: menuNo=" + menuNo);

    private static void Apply(MenuDto dto, Menu e)
    {
        if (dto.SubmoduleNo != null) e.SubmoduleNo = dto.SubmoduleNo.Value;
        if (dto.FormId != null) e.FormId = dto.FormId;
        if (dto.FormName != null) e.FormName = dto.FormName;
        if (dto.MenuDesc != null) e.MenuDesc = dto.MenuDesc;
        if (dto.MenuType != null) e.MenuType = dto.MenuType;
        if (dto.RoutePath != null) e.RoutePath = dto.RoutePath;
        if (dto.IconName != null) e.IconName = dto.IconName;
        if (dto.OrderSl != null) e.OrderSl = dto.OrderSl;
        if (dto.IsActive != null) e.IsActive = dto.IsActive.Value;
    }

    private static MenuDto ToDto(Menu e) => new()
    {
        MenuNo = e.MenuNo,
        SubmoduleNo = e.SubmoduleNo,
        FormId = e.FormId,
        FormName = e.FormName,
        MenuDesc = e.MenuDesc,
        MenuType = e.MenuType,
        RoutePath = e.RoutePath,
        IconName = e.IconName,
        OrderSl = e.OrderSl,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
