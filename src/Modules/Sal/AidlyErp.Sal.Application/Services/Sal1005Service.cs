using AidlyErp.Inv.Contracts;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sal.Domain;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sal.Application.Services;

public interface ISal1005Service
{
    Task<Sal1005LookupDto> GetLookupsAsync(CancellationToken ct = default);
    Task<List<Sal1005TerminalDto>> GetListAsync(CancellationToken ct = default);
    Task<Sal1005TerminalDto> SaveAsync(Sal1005TerminalDto dto, CancellationToken ct = default);
    Task DeleteAsync(long terminalNo, CancellationToken ct = default);
}

/// <summary>
/// SAL_1005 POS Terminal Setup — registers the tills a branch runs. A terminal names the warehouse
/// its sales relieve and the GL account its drawer cash lands in, so it must exist before any
/// cashier can open a session.
/// </summary>
public class Sal1005Service : ISal1005Service
{
    private const short Deleted = 0;

    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvLookup _invLookup;

    public Sal1005Service(ISalDbContext db, ICompanyBranchContext ctx, IInvLookup invLookup)
    {
        _db = db;
        _ctx = ctx;
        _invLookup = invLookup;
    }

    public async Task<Sal1005LookupDto> GetLookupsAsync(CancellationToken ct = default)
    {
        long companyNo = Company();
        long branchNo = Branch();

        var warehouses = await _invLookup.GetWarehousesAsync(companyNo, branchNo, ct);
        return new Sal1005LookupDto
        {
            Warehouses = warehouses.Select(w => new SalOptionDto { No = w.No, Name = w.Name }).ToList()
        };
    }

    public async Task<List<Sal1005TerminalDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        var rows = await _db.SalPosTerminals.AsNoTracking()
            .Where(t => t.CompanyNo == companyNo && t.BranchNo == branchNo && t.IsDeleted == Deleted)
            .OrderBy(t => t.TerminalNo)
            .ToListAsync(ct);

        var warehouseNames = await WarehouseNamesAsync(companyNo, branchNo, ct);
        return rows.Select(t => ToDto(t, warehouseNames.GetValueOrDefault(t.WarehouseNo))).ToList();
    }

    public async Task<Sal1005TerminalDto> SaveAsync(Sal1005TerminalDto dto, CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        if (string.IsNullOrWhiteSpace(dto.TerminalId)) throw new ValidationException("Terminal code is required");
        if (string.IsNullOrWhiteSpace(dto.TerminalName)) throw new ValidationException("Terminal name is required");
        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");

        await RequireBranchWarehouseAsync(companyNo, branchNo, dto.WarehouseNo, ct);

        SalPosTerminal e;
        if (dto.TerminalNo is > 0)
        {
            e = await _db.SalPosTerminals.FirstOrDefaultAsync(
                    t => t.TerminalNo == dto.TerminalNo.Value && t.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Terminal not found");

            if (e.CompanyNo != companyNo) throw new ValidationException("Terminal belongs to another company");

            // Repointing a till mid-shift would relieve the wrong warehouse for sales already rung up.
            if (e.WarehouseNo != dto.WarehouseNo && await HasOpenSessionAsync(e.TerminalNo, ct))
                throw new ValidationException("Close the open session before changing this terminal's warehouse");

            if (!string.Equals(e.TerminalId, dto.TerminalId, StringComparison.OrdinalIgnoreCase)
                && await CodeTakenAsync(branchNo, dto.TerminalId!, e.TerminalNo, ct))
            {
                throw new ValidationException($"Terminal code already exists: {dto.TerminalId}");
            }
        }
        else
        {
            if (await CodeTakenAsync(branchNo, dto.TerminalId!, null, ct))
                throw new ValidationException($"Terminal code already exists: {dto.TerminalId}");

            e = new SalPosTerminal
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.SalPosTerminals.Add(e);
        }

        e.TerminalId = dto.TerminalId!;
        e.TerminalName = dto.TerminalName!;
        e.WarehouseNo = dto.WarehouseNo;
        e.DeviceUuid = dto.DeviceUuid;
        e.ReceiptPrefix = dto.ReceiptPrefix;
        e.CashGlAccountNo = dto.CashGlAccountNo;
        e.Remarks = dto.Remarks;
        e.IsActive = dto.IsActive ?? 1;
        e.UpdatedBy = _ctx.CurrentUserNo();
        e.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var warehouseNames = await WarehouseNamesAsync(companyNo, branchNo, ct);
        return ToDto(e, warehouseNames.GetValueOrDefault(e.WarehouseNo));
    }

    public async Task DeleteAsync(long terminalNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var e = await _db.SalPosTerminals.FirstOrDefaultAsync(
                    t => t.TerminalNo == terminalNo && t.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Terminal not found");

        if (e.CompanyNo != companyNo) throw new ValidationException("Terminal belongs to another company");

        if (await HasOpenSessionAsync(terminalNo, ct))
            throw new ValidationException("This terminal has an open session — close it first");

        // Sessions are the audit trail for drawer cash; a terminal that has any must stay resolvable.
        bool hasHistory = await _db.SalPosSessions.AnyAsync(s => s.TerminalNo == terminalNo, ct);
        if (hasHistory) throw new ValidationException("This terminal has session history — deactivate it instead");

        e.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<bool> HasOpenSessionAsync(long terminalNo, CancellationToken ct) =>
        await _db.SalPosSessions.AnyAsync(
            s => s.TerminalNo == terminalNo && s.Status == 1 && s.IsDeleted == Deleted, ct);

    private async Task<bool> CodeTakenAsync(long branchNo, string terminalId, long? excludeNo, CancellationToken ct) =>
        await _db.SalPosTerminals.AnyAsync(
            t => t.BranchNo == branchNo && t.TerminalId == terminalId && t.IsDeleted == Deleted
                 && (excludeNo == null || t.TerminalNo != excludeNo), ct);

    private async Task RequireBranchWarehouseAsync(long companyNo, long branchNo, long warehouseNo,
                                                   CancellationToken ct)
    {
        var warehouses = await _invLookup.GetWarehousesAsync(companyNo, branchNo, ct);
        if (warehouses.All(w => w.No != warehouseNo))
            throw new ValidationException("Warehouse does not belong to your branch");
    }

    private async Task<Dictionary<long, string>> WarehouseNamesAsync(long companyNo, long branchNo,
                                                                     CancellationToken ct)
    {
        var warehouses = await _invLookup.GetWarehousesAsync(companyNo, branchNo, ct);
        return warehouses.ToDictionary(w => w.No, w => w.Name);
    }

    private static Sal1005TerminalDto ToDto(SalPosTerminal e, string? warehouseName) => new()
    {
        TerminalNo = e.TerminalNo,
        TerminalId = e.TerminalId,
        TerminalName = e.TerminalName,
        WarehouseNo = e.WarehouseNo,
        WarehouseName = warehouseName,
        DeviceUuid = e.DeviceUuid,
        ReceiptPrefix = e.ReceiptPrefix,
        CashGlAccountNo = e.CashGlAccountNo,
        Remarks = e.Remarks,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");
}
