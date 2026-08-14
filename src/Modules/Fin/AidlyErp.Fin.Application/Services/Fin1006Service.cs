using AidlyErp.Fin.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Domain;

namespace AidlyErp.Fin.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Fin1006Service — GL Mapping Rules
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1006Service
{
    Task<List<Fin1006GlMapDto>> GetListAsync(CancellationToken ct = default);
    Task<Fin1006GlMapDto> GetDetailAsync(long mapNo, CancellationToken ct = default);
    Task<Fin1006GlMapDto> SaveAsync(Fin1006GlMapDto dto, CancellationToken ct = default);
    Task DeleteAsync(long mapNo, CancellationToken ct = default);
}

public class Fin1006Service : IFin1006Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Fin1006Service(IFinDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1006GlMapDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var accounts = await _db.FinAccounts.AsNoTracking().Where(a => a.IsDeleted == 0).ToDictionaryAsync(a => a.AccountNo, ct);

        var rows = await _db.FinGlMaps
            .AsNoTracking()
            .Where(m => m.CompanyNo == companyNo && m.IsDeleted == 0)
            .OrderBy(m => m.EventType).ThenBy(m => m.LegKey)
            .ToListAsync(ct);

        return rows.Select(m => ToDto(m, accounts.GetValueOrDefault(m.AccountNo))).ToList();
    }

    public async Task<Fin1006GlMapDto> GetDetailAsync(long mapNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var map = await _db.FinGlMaps.AsNoTracking().FirstOrDefaultAsync(m => m.GlMapNo == mapNo && m.CompanyNo == companyNo && m.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"GL Map not found: {mapNo}");
        var acc = await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNo == map.AccountNo, ct);
        return ToDto(map, acc);
    }

    public async Task<Fin1006GlMapDto> SaveAsync(Fin1006GlMapDto dto, CancellationToken ct = default)
    {
        if (dto.MapNo.HasValue && dto.MapNo.Value > 0)
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            var map = await _db.FinGlMaps.FirstOrDefaultAsync(m => m.GlMapNo == dto.MapNo.Value && m.CompanyNo == companyNo && m.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"GL Map not found: {dto.MapNo}");

            // Validate account if changed
            if (dto.AccountNo != map.AccountNo)
            {
                var acc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == dto.AccountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
                    ?? throw new NotFoundException($"Account not found: {dto.AccountNo}");
                if (acc.IsActive != 1) throw new ValidationException("The mapped account is inactive");
                if (acc.IsPostable != 1) throw new ValidationException("A header account cannot be a posting target");
            }

            // Update uniqueness check (self-aware) — if key fields changed
            string newLegKey = dto.LegKey?.Trim().ToUpperInvariant() ?? map.LegKey;
            string newSubKey = dto.SubKey?.Trim() ?? map.SubKey ?? string.Empty;
            if (newLegKey != map.LegKey || newSubKey != (map.SubKey ?? string.Empty))
            {
                if (await _db.FinGlMaps.AnyAsync(m => m.CompanyNo == companyNo && m.EventType == map.EventType && m.LegKey == newLegKey && m.SubKey == newSubKey && m.GlMapNo != map.GlMapNo && m.IsDeleted == 0, ct))
                    throw new ValidationException($"A mapping already exists for {map.EventType} / {newLegKey}" + (string.IsNullOrEmpty(newSubKey) ? "" : $" / {newSubKey}"));
                map.LegKey = newLegKey;
                map.SubKey = newSubKey;
            }

            map.AccountNo = dto.AccountNo;
            map.IsActive = dto.IsActive;
            map.UpdatedBy = _ctx.CurrentUserNo(); map.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            var account = await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNo == map.AccountNo, ct);
            return ToDto(map, account);
        }
        else
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            if (string.IsNullOrWhiteSpace(dto.EventType)) throw new ValidationException("Event type is required");
            if (string.IsNullOrWhiteSpace(dto.LegKey)) throw new ValidationException("Leg key is required");

            var acc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == dto.AccountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Account not found: {dto.AccountNo}");
            if (acc.IsActive != 1) throw new ValidationException("The mapped account is inactive");
            if (acc.IsPostable != 1) throw new ValidationException("A header account cannot be a posting target");

            string eventType = dto.EventType.Trim();
            string legKey = dto.LegKey.Trim().ToUpperInvariant();
            string subKey = dto.SubKey?.Trim() ?? string.Empty;

            // Composite uniqueness check
            if (await _db.FinGlMaps.AnyAsync(m => m.CompanyNo == companyNo && m.EventType == eventType && m.LegKey == legKey && m.SubKey == subKey && m.IsDeleted == 0, ct))
                throw new ValidationException($"A mapping already exists for {eventType} / {legKey}" + (string.IsNullOrEmpty(subKey) ? "" : $" / {subKey}"));

            var map = new FinGlMap
            {
                CompanyNo = companyNo,
                BranchNo = _ctx.CurrentBranchNo(),
                EventType = eventType,
                LegKey = legKey,
                SubKey = subKey,
                AccountNo = dto.AccountNo,
                IsActive = dto.IsActive,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };

            _db.FinGlMaps.Add(map);
            await _db.SaveChangesAsync(ct);
            return ToDto(map, acc);
        }
    }

    public async Task DeleteAsync(long mapNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var map = await _db.FinGlMaps.FirstOrDefaultAsync(m => m.GlMapNo == mapNo && m.CompanyNo == companyNo && m.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"GL Map not found: {mapNo}");
        map.IsDeleted = 1; map.IsActive = 0;
        map.DeletedBy = _ctx.CurrentUserNo(); map.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Fin1006GlMapDto ToDto(FinGlMap m, FinAccount? a) => new()
    {
        MapNo = m.GlMapNo,
        EventType = m.EventType,
        LegKey = m.LegKey,
        SubKey = m.SubKey,
        AccountNo = m.AccountNo,
        IsActive = m.IsActive ?? 0,
        RowVersion = m.RowVersion,
        AccountCode = a?.AccountCode,
        AccountName = a?.AccountName
    };
}
