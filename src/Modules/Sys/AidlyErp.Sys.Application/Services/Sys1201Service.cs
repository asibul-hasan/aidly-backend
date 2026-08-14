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

namespace AidlyErp.Sys.Application.Services;

/// <summary>
/// Invalidates the RBAC URL→form cache. Implemented in the API layer (where the middleware
/// lives) and injected here so menu edits take effect without a restart.
/// </summary>
public interface IPathFormCacheInvalidator
{
    void EvictPathFormCache();
}

public interface ISys1201Service
{
    Task<List<Sys1201ModuleDto>> ListModulesAsync(CancellationToken cancellationToken = default);
    Task<Sys1201ModuleDto> SaveModuleAsync(Sys1201ModuleDto dto, CancellationToken cancellationToken = default);
    Task DeleteModuleAsync(long moduleNo, CancellationToken cancellationToken = default);
    Task<List<SysLookupDto>> GetModuleOptionsAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1201SubmoduleDto>> ListSubmodulesAsync(CancellationToken cancellationToken = default);
    Task<Sys1201SubmoduleDto> SaveSubmoduleAsync(Sys1201SubmoduleDto dto, CancellationToken cancellationToken = default);
    Task DeleteSubmoduleAsync(long submoduleNo, CancellationToken cancellationToken = default);
    Task<List<SysLookupDto>> GetSubmoduleOptionsAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1201MenuDto>> ListMenusAsync(CancellationToken cancellationToken = default);
    Task<Sys1201MenuDto> SaveMenuAsync(Sys1201MenuDto dto, CancellationToken cancellationToken = default);
    Task DeleteMenuAsync(long menuNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// SYS1201 Menu Builder — the global Module → Submodule → Menu catalogue. Not company-scoped:
/// this is the product's form inventory, which companies then subscribe to via
/// <c>sys_enroll_menu</c>.
/// </summary>
public class Sys1201Service : ISys1201Service
{
    private const short Deleted = 0;

    /// <summary>Sorts nulls last, matching the <c>NULLS LAST</c> ordering used by the SQL side.</summary>
    private const int OrderSlNullsLast = int.MaxValue;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly IPathFormCacheInvalidator _pathFormCache;

    public Sys1201Service(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                          IPathFormCacheInvalidator pathFormCache)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _pathFormCache = pathFormCache;
    }

    // ─── Modules ─────────────────────────────────────────────────────────────

    public async Task<List<Sys1201ModuleDto>> ListModulesAsync(CancellationToken cancellationToken = default)
    {
        var modules = await LiveModulesAsync(cancellationToken);

        return modules
            .OrderBy(m => m.OrderSl ?? OrderSlNullsLast)
            .ThenBy(m => m.ModuleName, StringComparer.Ordinal)
            .Select(ToModuleDto)
            .ToList();
    }

    public Task<Sys1201ModuleDto> SaveModuleAsync(Sys1201ModuleDto dto,
                                                  CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var code = Required(dto.ModuleCode, "Module code");
            SysModule e;

            if (dto.ModuleNo != null)
            {
                e = await _db.SysModules.FirstOrDefaultAsync(m => m.ModuleNo == dto.ModuleNo && m.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Module not found");

                // Only re-check uniqueness when the code actually changed.
                if (!string.Equals(code, e.ModuleCode, StringComparison.OrdinalIgnoreCase)
                    && await ModuleCodeExistsAsync(code, ct))
                {
                    throw new ValidationException("Module code already exists: " + code);
                }
            }
            else
            {
                if (await ModuleCodeExistsAsync(code, ct))
                {
                    throw new ValidationException("Module code already exists: " + code);
                }

                e = new SysModule();
                _db.SysModules.Add(e);
            }

            e.ModuleCode = code;
            e.ModuleName = dto.ModuleName ?? string.Empty;
            e.ModuleDesc = dto.ModuleDesc;
            e.ModuleIcon = dto.ModuleIcon;
            e.ModuleRoute = dto.ModuleRoute;
            e.OrderSl = dto.OrderSl ?? 0;
            e.IsActive = dto.IsActive ?? 1;

            await _db.SaveChangesAsync(ct);
            return ToModuleDto(e);
        }, cancellationToken);

    public Task DeleteModuleAsync(long moduleNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var e = await _db.SysModules.FirstOrDefaultAsync(m => m.ModuleNo == moduleNo && m.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Module not found");

            if (await _db.SysSubmodules.AnyAsync(s => s.ModuleNo == moduleNo && s.IsDeleted == Deleted, ct))
            {
                throw new ValidationException("Remove the module's submodules first");
            }

            e.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    public async Task<List<SysLookupDto>> GetModuleOptionsAsync(CancellationToken cancellationToken = default)
    {
        var modules = await LiveModulesAsync(cancellationToken);

        return modules
            .OrderBy(m => m.OrderSl ?? OrderSlNullsLast)
            .Select(m => new SysLookupDto(m.ModuleNo, m.ModuleName))
            .ToList();
    }

    // ─── Submodules ──────────────────────────────────────────────────────────

    public async Task<List<Sys1201SubmoduleDto>> ListSubmodulesAsync(CancellationToken cancellationToken = default)
    {
        var moduleNames = await ModuleNameMapAsync(cancellationToken);
        var submodules = await LiveSubmodulesAsync(cancellationToken);

        return submodules
            .OrderBy(s => s.ModuleNo)
            .ThenBy(s => s.OrderSl ?? OrderSlNullsLast)
            .Select(s => ToSubmoduleDto(s, moduleNames))
            .ToList();
    }

    public Task<Sys1201SubmoduleDto> SaveSubmoduleAsync(Sys1201SubmoduleDto dto,
                                                        CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var moduleNo = dto.ModuleNo ?? throw new ValidationException("Module is required");
            var code = Required(dto.SubmoduleCode, "Submodule code");

            _ = await _db.SysModules.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModuleNo == moduleNo && m.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Module not found: moduleNo=" + moduleNo);

            SysSubmodule e;

            if (dto.SubmoduleNo != null)
            {
                e = await _db.SysSubmodules
                        .FirstOrDefaultAsync(s => s.SubmoduleNo == dto.SubmoduleNo && s.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Submodule not found");

                // Uniqueness is per (module, code) — re-check when either half changed.
                var keyChanged = e.ModuleNo != moduleNo
                                 || !string.Equals(code, e.SubmoduleCode, StringComparison.OrdinalIgnoreCase);

                if (keyChanged && await SubmoduleCodeExistsAsync(moduleNo, code, ct))
                {
                    throw new ValidationException("Submodule code already exists in this module");
                }
            }
            else
            {
                if (await SubmoduleCodeExistsAsync(moduleNo, code, ct))
                {
                    throw new ValidationException("Submodule code already exists in this module");
                }

                e = new SysSubmodule();
                _db.SysSubmodules.Add(e);
            }

            e.ModuleNo = moduleNo;
            e.SubmoduleCode = code;
            e.SubmoduleName = dto.SubmoduleName ?? string.Empty;
            e.SubmoduleIcon = dto.SubmoduleIcon;
            e.SubmoduleRoute = dto.SubmoduleRoute;
            e.OrderSl = dto.OrderSl ?? 0;
            e.IsActive = dto.IsActive ?? 1;

            await _db.SaveChangesAsync(ct);

            return ToSubmoduleDto(e, await ModuleNameMapAsync(ct));
        }, cancellationToken);

    public Task DeleteSubmoduleAsync(long submoduleNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var e = await _db.SysSubmodules
                        .FirstOrDefaultAsync(s => s.SubmoduleNo == submoduleNo && s.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Submodule not found");

            if (await _db.Menus.AnyAsync(m => m.SubmoduleNo == submoduleNo && m.IsDeleted == Deleted, ct))
            {
                throw new ValidationException("Remove the submodule's menus first");
            }

            e.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    public async Task<List<SysLookupDto>> GetSubmoduleOptionsAsync(CancellationToken cancellationToken = default)
    {
        var moduleNames = await ModuleNameMapAsync(cancellationToken);
        var submodules = await LiveSubmodulesAsync(cancellationToken);

        return submodules
            .OrderBy(s => s.ModuleNo)
            .ThenBy(s => s.OrderSl ?? OrderSlNullsLast)
            .Select(s => new SysLookupDto(
                s.SubmoduleNo,
                (moduleNames.TryGetValue(s.ModuleNo, out var moduleName) ? moduleName : "?") + " / " + s.SubmoduleName))
            .ToList();
    }

    // ─── Menus ───────────────────────────────────────────────────────────────

    public async Task<List<Sys1201MenuDto>> ListMenusAsync(CancellationToken cancellationToken = default)
    {
        var subs = (await LiveSubmodulesAsync(cancellationToken)).ToDictionary(s => s.SubmoduleNo);
        var moduleNames = await ModuleNameMapAsync(cancellationToken);

        var menus = await _db.Menus.AsNoTracking().Where(m => m.IsDeleted == Deleted).ToListAsync(cancellationToken);

        return menus
            .OrderBy(m => m.SubmoduleNo)
            .ThenBy(m => m.OrderSl ?? OrderSlNullsLast)
            .Select(m => ToMenuDto(m, subs, moduleNames))
            .ToList();
    }

    public Task<Sys1201MenuDto> SaveMenuAsync(Sys1201MenuDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var submoduleNo = dto.SubmoduleNo ?? throw new ValidationException("Submodule is required");
            var formId = Required(dto.FormId, "Form ID");

            _ = await _db.SysSubmodules.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SubmoduleNo == submoduleNo && s.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Submodule not found: submoduleNo=" + submoduleNo);

            Menu e;

            if (dto.MenuNo != null)
            {
                e = await _db.Menus.FirstOrDefaultAsync(m => m.MenuNo == dto.MenuNo && m.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Menu not found");

                if (!string.Equals(formId, e.FormId, StringComparison.OrdinalIgnoreCase)
                    && await FormIdExistsAsync(formId, ct))
                {
                    throw new ValidationException("Form ID already exists: " + formId);
                }
            }
            else
            {
                if (await FormIdExistsAsync(formId, ct))
                {
                    throw new ValidationException("Form ID already exists: " + formId);
                }

                e = new Menu();
                _db.Menus.Add(e);
            }

            e.SubmoduleNo = submoduleNo;
            e.FormId = formId;
            e.FormName = dto.FormName ?? string.Empty;
            e.MenuDesc = dto.MenuDesc;
            e.MenuType = dto.MenuType;
            e.RoutePath = dto.RoutePath;
            e.IconName = dto.IconName;
            e.OrderSl = dto.OrderSl ?? 0;
            e.IsActive = dto.IsActive ?? 1;

            await _db.SaveChangesAsync(ct);

            // The route table just changed — drop the memoized URL→form map so RBAC resolves the
            // new/edited route on the very next request.
            _pathFormCache.EvictPathFormCache();

            var subs = (await LiveSubmodulesAsync(ct)).ToDictionary(s => s.SubmoduleNo);
            return ToMenuDto(e, subs, await ModuleNameMapAsync(ct));
        }, cancellationToken);

    public Task DeleteMenuAsync(long menuNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var e = await _db.Menus.FirstOrDefaultAsync(m => m.MenuNo == menuNo && m.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Menu not found");

            e.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);

            _pathFormCache.EvictPathFormCache();
        }, cancellationToken);

    // ── helpers ───────────────────────────────────────────────────────────────

    private Task<List<SysModule>> LiveModulesAsync(CancellationToken ct) =>
        _db.SysModules.AsNoTracking().Where(m => m.IsDeleted == Deleted).ToListAsync(ct);

    private Task<List<SysSubmodule>> LiveSubmodulesAsync(CancellationToken ct) =>
        _db.SysSubmodules.AsNoTracking().Where(s => s.IsDeleted == Deleted).ToListAsync(ct);

    private async Task<Dictionary<long, string>> ModuleNameMapAsync(CancellationToken ct) =>
        (await LiveModulesAsync(ct)).ToDictionary(m => m.ModuleNo, m => m.ModuleName ?? string.Empty);

    private Task<bool> ModuleCodeExistsAsync(string code, CancellationToken ct) =>
        _db.SysModules.AnyAsync(m => m.ModuleCode.ToUpper() == code.ToUpper() && m.IsDeleted == Deleted, ct);

    private Task<bool> SubmoduleCodeExistsAsync(long moduleNo, string code, CancellationToken ct) =>
        _db.SysSubmodules.AnyAsync(s => s.ModuleNo == moduleNo
                                        && s.SubmoduleCode.ToUpper() == code.ToUpper()
                                        && s.IsDeleted == Deleted, ct);

    private Task<bool> FormIdExistsAsync(string formId, CancellationToken ct) =>
        _db.Menus.AnyAsync(m => m.FormId.ToUpper() == formId.ToUpper() && m.IsDeleted == Deleted, ct);

    private static string Required(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(fieldName + " is required");
        }

        return value.Trim();
    }

    private static Sys1201ModuleDto ToModuleDto(SysModule e) => new()
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

    private static Sys1201SubmoduleDto ToSubmoduleDto(SysSubmodule e, IReadOnlyDictionary<long, string> moduleNames) => new()
    {
        SubmoduleNo = e.SubmoduleNo,
        ModuleNo = e.ModuleNo,
        ModuleName = moduleNames.TryGetValue(e.ModuleNo, out var name) ? name : null,
        SubmoduleCode = e.SubmoduleCode,
        SubmoduleName = e.SubmoduleName,
        SubmoduleIcon = e.SubmoduleIcon,
        SubmoduleRoute = e.SubmoduleRoute,
        OrderSl = e.OrderSl,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };

    private static Sys1201MenuDto ToMenuDto(Menu e, IReadOnlyDictionary<long, SysSubmodule> subs,
                                            IReadOnlyDictionary<long, string> moduleNames)
    {
        SysSubmodule? sub = e.SubmoduleNo.HasValue && subs.TryGetValue(e.SubmoduleNo.Value, out var s) ? s : null;

        return new Sys1201MenuDto
        {
            MenuNo = e.MenuNo,
            SubmoduleNo = e.SubmoduleNo,
            SubmoduleName = sub?.SubmoduleName,
            ModuleName = sub != null && moduleNames.TryGetValue(sub.ModuleNo, out var moduleName) ? moduleName : null,
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
}
