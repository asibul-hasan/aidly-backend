using AidlyErp.Sal.Application.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Application.Contract;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Domain;
using AidlyErp.Sys.Domain;

namespace AidlyErp.Sal.Application.Services;

public interface ISal1103Service
{
    Task<List<Sal1103ReturnDto>> GetListAsync(CancellationToken ct = default);
    Task<Sal1103ReturnDto> GetDetailAsync(long returnNo, CancellationToken ct = default);
    Task<Sal1103ReturnDto> SaveAsync(Sal1103ReturnDto dto, CancellationToken ct = default);
    Task<Sal1103ReturnDto> PostAsync(long returnNo, CancellationToken ct = default);
    Task DeleteAsync(long returnNo, CancellationToken ct = default);
}

public class Sal1103Service : ISal1103Service
{
    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly ISalArLedgerService _arLedger;

    private const short StDraft = 1, StPosted = 2, StCancelled = 3;

    public Sal1103Service(ISalDbContext db, ICompanyBranchContext ctx, ISalArLedgerService arLedger)
    {
        _db = db;
        _ctx = ctx;
        _arLedger = arLedger;
    }

    public async Task<List<Sal1103ReturnDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var custs = await _db.SalCustomers.AsNoTracking().Where(c => c.IsDeleted == 0).ToDictionaryAsync(c => c.CustomerNo, c => c.CustomerName, ct);

        var list = await _db.SalReturns
            .AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.IsDeleted == 0)
            .OrderByDescending(r => r.ReturnNo)
            .ToListAsync(ct);

        return list.Select(r => ToDto(r, custs.ContainsKey(r.CustomerNo) ? custs[r.CustomerNo] : null, null)).ToList();
    }

    public async Task<Sal1103ReturnDto> GetDetailAsync(long returnNo, CancellationToken ct = default)
    {
        var r = await _db.SalReturns.AsNoTracking().FirstOrDefaultAsync(x => x.ReturnNo == returnNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Sales return not found: {returnNo}");

        var custName = await _db.SalCustomers.Where(c => c.CustomerNo == r.CustomerNo).Select(c => c.CustomerName).FirstOrDefaultAsync(ct);
        var lines = await GetLinesAsync(returnNo, ct);

        return ToDto(r, custName, lines);
    }

    public async Task<Sal1103ReturnDto> SaveAsync(Sal1103ReturnDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.CustomerNo <= 0) throw new ValidationException("Customer is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Return lines are required");

        decimal totalAmount = dto.Lines.Sum(l => l.Quantity * l.UnitPrice);
        decimal grandTotal = totalAmount + dto.TaxAmount;

        SalReturn ret;
        if (dto.ReturnNo.HasValue && dto.ReturnNo.Value > 0)
        {
            ret = await _db.SalReturns.FirstOrDefaultAsync(x => x.ReturnNo == dto.ReturnNo.Value && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Return not found: {dto.ReturnNo}");
            if (ret.Status != StDraft) throw new ValidationException("Only a Draft return can be edited");
            ret.TotalAmount = totalAmount;
            ret.TaxAmount = dto.TaxAmount;
            ret.GrandTotal = grandTotal;
            ret.Reason = dto.Reason;
            ret.Remarks = dto.Remarks;
            ret.UpdatedBy = _ctx.CurrentUserNo(); ret.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            ret = new SalReturn
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                ReturnId = $"RET-{DateTime.UtcNow:yyyyMMddHHmmss}",
                ReturnDate = dto.ReturnDate != default ? dto.ReturnDate : DateTime.UtcNow.Date,
                InvoiceNo = dto.InvoiceNo,
                CustomerNo = dto.CustomerNo,
                WarehouseNo = dto.WarehouseNo > 0 ? dto.WarehouseNo : 1,
                TotalAmount = totalAmount,
                TaxAmount = dto.TaxAmount,
                GrandTotal = grandTotal,
                Status = StDraft,
                Reason = dto.Reason,
                Remarks = dto.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.SalReturns.Add(ret);
            await _db.SaveChangesAsync(ct);
            ret.ReturnId = $"RET-{ret.ReturnNo:D6}";
        }

        await ReplaceLinesAsync(ret.ReturnNo, dto.Lines, ct);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(ret.ReturnNo, ct);
    }

    public async Task<Sal1103ReturnDto> PostAsync(long returnNo, CancellationToken ct = default)
    {
        var ret = await _db.SalReturns.FirstOrDefaultAsync(x => x.ReturnNo == returnNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Return not found: {returnNo}");

        if (ret.Status == StPosted) return await GetDetailAsync(returnNo, ct);
        if (ret.Status != StDraft) throw new ValidationException("Only a Draft return can be posted");

        await _arLedger.CreditAsync(ret.CustomerNo, ret.GrandTotal, "RETURN", ret.ReturnNo, ret.ReturnId, $"Sales Return {ret.ReturnId}", ct);

        var payload = new GlPostingPayload
        {
            VoucherDate = ret.ReturnDate,
            Narration = $"Sales Return {ret.ReturnId}",
            BranchNo = ret.BranchNo,
            Legs = new List<GlPostingPayload.Leg>
            {
                new() { LegKey = "REVENUE_RETURN", Amount = ret.TotalAmount, DrCr = "dr" },
                new() { LegKey = "VAT_OUTPUT", Amount = ret.TaxAmount, DrCr = "dr" },
                new() { LegKey = "RECEIVABLE", Amount = ret.GrandTotal, DrCr = "cr", PartyType = 1, PartyNo = ret.CustomerNo }
            }
        };

        var ev = new EventOutbox
        {
            CompanyNo = ret.CompanyNo,
            BranchNo = ret.BranchNo,
            EventType = "SalesReturnPosted",
            AggregateType = "SalReturn",
            AggregateId = ret.ReturnNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1, CreatedAt = DateTime.UtcNow
        };
        _db.EventOutboxes.Add(ev);

        ret.Status = StPosted;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(returnNo, ct);
    }

    public async Task DeleteAsync(long returnNo, CancellationToken ct = default)
    {
        var ret = await _db.SalReturns.FirstOrDefaultAsync(x => x.ReturnNo == returnNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Return not found: {returnNo}");

        if (ret.Status != StDraft) throw new ValidationException("Only a Draft return can be deleted");

        ret.IsDeleted = 1; ret.IsActive = 0;
        ret.DeletedBy = _ctx.CurrentUserNo(); ret.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ReplaceLinesAsync(long returnNo, List<Sal1103LineDto> lines, CancellationToken ct)
    {
        var existing = await _db.SalReturnDtls.Where(l => l.ReturnNo == returnNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var e in existing) { e.IsDeleted = 1; e.DeletedBy = _ctx.CurrentUserNo(); e.DeletedAt = DateTime.UtcNow; }

        foreach (var dto in lines)
        {
            var entity = new SalReturnDtl
            {
                ReturnNo = returnNo,
                ItemNo = dto.ItemNo,
                UomNo = dto.UomNo,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                UnitCost = dto.UnitCost,
                LineTotal = dto.Quantity * dto.UnitPrice,
                Remarks = dto.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.SalReturnDtls.Add(entity);
        }
    }

    private async Task<List<Sal1103LineDto>> GetLinesAsync(long returnNo, CancellationToken ct)
    {
        return await _db.SalReturnDtls
            .AsNoTracking()
            .Where(l => l.ReturnNo == returnNo && l.IsDeleted == 0)
            .Select(l => new Sal1103LineDto
            {
                ReturnDtlNo = l.ReturnDtlNo,
                ItemNo = l.ItemNo,
                UomNo = l.UomNo,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                UnitCost = l.UnitCost,
                LineTotal = l.LineTotal,
                Remarks = l.Remarks
            })
            .ToListAsync(ct);
    }

    private static Sal1103ReturnDto ToDto(SalReturn r, string? custName, List<Sal1103LineDto>? lines) => new()
    {
        ReturnNo = r.ReturnNo,
        ReturnId = r.ReturnId,
        ReturnDate = r.ReturnDate,
        InvoiceNo = r.InvoiceNo,
        CustomerNo = r.CustomerNo,
        CustomerName = custName,
        WarehouseNo = r.WarehouseNo,
        TotalAmount = r.TotalAmount,
        TaxAmount = r.TaxAmount,
        GrandTotal = r.GrandTotal,
        Status = r.Status,
        Reason = r.Reason,
        Remarks = r.Remarks,
        IsActive = r.IsActive,
        RowVersion = r.RowVersion,
        Lines = lines ?? new()
    };
}
