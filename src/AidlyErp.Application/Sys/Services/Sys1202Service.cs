using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Domain.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Application.Sys.Services;

public interface ISys1202Service
{
    Task<List<SysLookupDto>> GetCompaniesAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1202MenuRow>> GetMenusAsync(long? companyNo, CancellationToken cancellationToken = default);

    Task<List<Sys1202MenuRow>> SaveEnrollmentsAsync(long? companyNo, List<Sys1202MenuRow>? rows,
                                                    CancellationToken cancellationToken = default);
}

/// <summary>
/// SYS1202 Menu Enrollment — grant a client company the menus (forms) it has purchased
/// (<c>sys_enroll_menu</c>, company-wide rows where <c>branch_no IS NULL</c>). An active
/// enrolment is the licensing gate the RBAC layer joins against.
/// </summary>
public class Sys1202Service : ISys1202Service
{
    private const short Deleted = 0;

    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1202Service> _logger;

    public Sys1202Service(IApplicationDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1202Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    public async Task<List<SysLookupDto>> GetCompaniesAsync(CancellationToken cancellationToken = default) =>
        await _db.Companies
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.IsDeleted == Deleted)
            .OrderBy(c => c.CompanyNo)
            .Select(c => new SysLookupDto(c.CompanyNo, c.CompanyName))
            .ToListAsync(cancellationToken);

    public async Task<List<Sys1202MenuRow>> GetMenusAsync(long? companyNo,
                                                          CancellationToken cancellationToken = default)
    {
        await RequireCompanyAsync(companyNo, cancellationToken);

        var rows = await FindMenuCatalogForCompanyAsync(companyNo!.Value, cancellationToken);
        return rows.Select(ToRow).ToList();
    }

    public Task<List<Sys1202MenuRow>> SaveEnrollmentsAsync(long? companyNo, List<Sys1202MenuRow>? rows,
                                                           CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            await RequireCompanyAsync(companyNo, ct);

            if (rows == null) return await GetMenusAsync(companyNo, ct);

            var existing = await _db.EnrollMenus
                .Where(e => e.CompanyNo == companyNo && e.BranchNo == null && e.IsDeleted == Deleted)
                .ToListAsync(ct);

            var byMenu = new Dictionary<long, EnrollMenu>();
            foreach (var e in existing) byMenu.TryAdd(e.MenuNo, e);

            var currentUser = _ctx.CurrentUserNo();
            var kept = new HashSet<long>();

            foreach (var row in rows)
            {
                // Rows the grid left unticked are simply skipped; the un-enrol pass below
                // soft-deletes whatever they used to have.
                if (row.MenuNo == null || row.Enrolled != true) continue;

                var lifetime = row.IsLifetime ?? 0;

                if (lifetime == 0 && row.EnrollStartDate != null && row.EnrollEndDate != null
                    && row.EnrollEndDate < row.EnrollStartDate)
                {
                    throw new ValidationException("End date cannot be before start date for menu " + row.FormId);
                }

                if (!byMenu.TryGetValue(row.MenuNo.Value, out var e))
                {
                    e = new EnrollMenu
                    {
                        CompanyNo = companyNo!.Value,
                        BranchNo = null,
                        MenuNo = row.MenuNo.Value
                    };
                    _db.EnrollMenus.Add(e);
                    byMenu[row.MenuNo.Value] = e;
                }

                e.IsLifetime = lifetime;

                // A lifetime enrolment carries no window — clear the dates rather than leaving
                // stale ones behind.
                e.EnrollStartDate = lifetime == 1 ? null : row.EnrollStartDate;
                e.EnrollEndDate = lifetime == 1 ? null : row.EnrollEndDate;
                e.IsActive = 1;

                kept.Add(row.MenuNo.Value);
            }

            // Un-enrol menus that were cleared.
            foreach (var e in existing)
            {
                if (!kept.Contains(e.MenuNo))
                {
                    e.PerformSoftDelete(currentUser);
                }
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("SYS1202: reconciled {Count} enrolment(s) for companyNo={CompanyNo}",
                kept.Count, companyNo);

            return await GetMenusAsync(companyNo, ct);
        }, cancellationToken);

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task RequireCompanyAsync(long? companyNo, CancellationToken ct)
    {
        if (companyNo == null) throw new ValidationException("Company is required");

        var exists = await _db.Companies
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(c => c.CompanyNo == companyNo && c.IsDeleted == Deleted, ct);

        if (!exists)
        {
            throw new NotFoundException("Company not found: companyNo=" + companyNo);
        }
    }

    private static Sys1202MenuRow ToRow(MenuEnrollRow r) => new()
    {
        MenuNo = r.MenuNo,
        FormId = r.FormId,
        FormName = r.FormName,
        ModuleName = r.ModuleName,
        ModuleCode = r.ModuleCode,
        SubmoduleName = r.SubmoduleName,
        ModuleSerial = r.ModuleSerial,
        EnrollMenuNo = r.EnrollMenuNo,
        Enrolled = r.EnrollMenuNo != null,
        IsLifetime = r.IsLifetime ?? 0,
        EnrollStartDate = r.EnrollStartDate,
        EnrollEndDate = r.EnrollEndDate
    };

    /// <summary>
    /// Every menu with this company's enrolment LEFT-joined on, so unenrolled forms still appear
    /// (with a null <c>enroll_menu_no</c>) and can be ticked. Ported verbatim from
    /// <c>EnrollMenuRepository.findMenuCatalogForCompany</c>.
    /// </summary>
    private Task<List<MenuEnrollRow>> FindMenuCatalogForCompanyAsync(long companyNo, CancellationToken ct) =>
        _db.Database.SqlQueryRaw<MenuEnrollRow>(
            """
            SELECT m.menu_no            AS "MenuNo",
                   m.form_id            AS "FormId",
                   m.form_name          AS "FormName",
                   mo.module_name       AS "ModuleName",
                   mo.module_code       AS "ModuleCode",
                   sm.submodule_name    AS "SubmoduleName",
                   mo.order_sl          AS "ModuleSerial",
                   em.enroll_menu_no    AS "EnrollMenuNo",
                   em.is_lifetime       AS "IsLifetime",
                   em.enroll_start_date AS "EnrollStartDate",
                   em.enroll_end_date   AS "EnrollEndDate"
            FROM   sys_menu m
            JOIN   sys_submodule sm ON sm.submodule_no = m.submodule_no
                                AND sm.is_active = 1 AND sm.is_deleted = 0
            JOIN   sys_module    mo ON mo.module_no = sm.module_no
                                AND mo.is_active = 1 AND mo.is_deleted = 0
            LEFT JOIN sys_enroll_menu em ON em.menu_no = m.menu_no
                                AND em.company_no = {0}
                                AND em.branch_no IS NULL
                                AND em.is_deleted = 0
            WHERE  m.is_active = 1 AND m.is_deleted = 0
            ORDER  BY mo.order_sl NULLS LAST, mo.module_name,
                      sm.order_sl NULLS LAST, sm.submodule_name,
                      m.order_sl  NULLS LAST, m.form_name
            """, companyNo).ToListAsync(ct);

    /// <summary>Projection for <see cref="FindMenuCatalogForCompanyAsync"/>; names match the SQL aliases.</summary>
    private sealed class MenuEnrollRow
    {
        public long? MenuNo { get; set; }
        public string? FormId { get; set; }
        public string? FormName { get; set; }
        public string? ModuleName { get; set; }
        public string? ModuleCode { get; set; }
        public string? SubmoduleName { get; set; }
        public int? ModuleSerial { get; set; }
        public long? EnrollMenuNo { get; set; }
        public short? IsLifetime { get; set; }
        public DateOnly? EnrollStartDate { get; set; }
        public DateOnly? EnrollEndDate { get; set; }
    }
}
