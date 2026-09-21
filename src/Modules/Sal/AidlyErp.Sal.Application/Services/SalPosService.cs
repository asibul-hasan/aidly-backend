using System.Text.Json;
using AidlyErp.Fin.Contracts;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Sal.Domain;
using AidlyErp.Shared.Core;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sal.Application.Services;

public interface ISalPosService
{
    Task<SalPosSessionDto> OpenAsync(long terminalNo, decimal openingFloat, CancellationToken ct = default);
    Task<SalPosSessionDto?> CurrentAsync(long terminalNo, CancellationToken ct = default);

    /// <summary>Sessions for this branch, newest first — the closing form's picker and its history.</summary>
    Task<List<SalPosSessionDto>> GetListAsync(short? status, CancellationToken ct = default);

    Task<SalPosSessionSummaryDto> SummaryAsync(long sessionNo, CancellationToken ct = default);
    Task<SalPosSessionDto> CloseAsync(SalCloseSessionRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// The open drawer a POS sale must belong to. Throws when there is none — by decision, the till
    /// prompts the cashier to open a session rather than opening one behind their back.
    /// </summary>
    Task<SalPosSession> RequireOpenSessionAsync(long terminalNo, CancellationToken ct = default);
}

/// <summary>
/// Drawer sessions: open a till, see what it has taken, close it against a physical cash count.
/// The close is what makes cash accountable — it compares counted notes to the tenders actually
/// recorded and posts the difference as over/short.
/// </summary>
public class SalPosService : ISalPosService
{
    private const short Deleted = 0;
    private const short StOpen = 1, StClosed = 3;

    private const short MethodCash = 1, MethodCard = 2, MethodMobile = 3;
    private const short SaleConfirmed = 2;

    private const string DocType = "SAL_SESSION";

    /// <summary>
    /// Rounding slack, not a policy allowance — anything above this is a real discrepancy and needs
    /// a supervisor's sign-off. Make it configurable per company if a business wants a wider band.
    /// </summary>
    private const decimal VarianceTolerance = 0.01m;

    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IDocSequenceGenerator _docSeq;

    public SalPosService(ISalDbContext db, ICompanyBranchContext ctx, IDocSequenceGenerator docSeq)
    {
        _db = db;
        _ctx = ctx;
        _docSeq = docSeq;
    }

    public async Task<SalPosSessionDto> OpenAsync(long terminalNo, decimal openingFloat,
                                                  CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        if (openingFloat < 0) throw new ValidationException("Opening float cannot be negative");

        var terminal = await RequireTerminalAsync(terminalNo, ct);
        if (terminal.IsActive != 1) throw new ValidationException("This terminal is inactive");

        // Checked here for a clear message; the partial unique index is what actually guarantees it
        // when two cashiers hit Open at the same moment.
        if (await _db.SalPosSessions.AnyAsync(s => s.TerminalNo == terminalNo && s.Status == StOpen
                                                   && s.IsDeleted == Deleted, ct))
        {
            throw new ValidationException("This terminal already has an open session");
        }

        var session = new SalPosSession
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            TerminalNo = terminalNo,
            SessionId = await _docSeq.NextAsync(companyNo, branchNo, DocType, "SES", 6, ct),
            CashierUserNo = CurrentUser(),
            OpenedAt = DateTime.UtcNow,
            OpeningFloat = openingFloat,
            Status = StOpen,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };

        _db.SalPosSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return ToDto(session, terminal);
    }

    public async Task<SalPosSessionDto?> CurrentAsync(long terminalNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var session = await _db.SalPosSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TerminalNo == terminalNo && s.CompanyNo == companyNo
                                      && s.Status == StOpen && s.IsDeleted == Deleted, ct);

        if (session is null) return null;

        var terminal = await _db.SalPosTerminals.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TerminalNo == terminalNo, ct);

        return ToDto(session, terminal);
    }

    public async Task<SalPosSessionSummaryDto> SummaryAsync(long sessionNo, CancellationToken ct = default)
    {
        var session = await RequireSessionAsync(sessionNo, ct);
        var totals = await TenderTotalsAsync(sessionNo, ct);

        var terminal = await _db.SalPosTerminals.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TerminalNo == session.TerminalNo, ct);

        decimal expectedCash = totals.GetValueOrDefault(MethodCash);

        var sales = await SessionSalesAsync(sessionNo, ct);

        return new SalPosSessionSummaryDto
        {
            Session = ToDto(session, terminal),
            Tenders = totals.OrderBy(t => t.Key)
                .Select(t => new SalPosTenderTotalDto
                {
                    PaymentMethod = t.Key,
                    MethodName = MethodName(t.Key),
                    Amount = t.Value
                }).ToList(),
            ExpectedCash = expectedCash,
            ExpectedDrawer = session.OpeningFloat + expectedCash,
            TotalSales = sales.Total,
            TotalReturns = session.TotalReturns,
            InvoiceCount = sales.Count,
            NonCashTotal = totals.Where(t => t.Key != MethodCash).Sum(t => t.Value),
            DueTotal = sales.Due
        };
    }

    public async Task<List<SalPosSessionDto>> GetListAsync(short? status, CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        var sessions = await _db.SalPosSessions.AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.BranchNo == branchNo && s.IsDeleted == Deleted
                        && (status == null || s.Status == status))
            .OrderByDescending(s => s.SessionNo)
            .Take(200)
            .ToListAsync(ct);

        if (sessions.Count == 0) return new List<SalPosSessionDto>();

        var terminalNos = sessions.Select(s => s.TerminalNo).Distinct().ToList();
        var terminals = await _db.SalPosTerminals.AsNoTracking()
            .Where(t => terminalNos.Contains(t.TerminalNo))
            .ToDictionaryAsync(t => t.TerminalNo, ct);

        return sessions.Select(s => ToDto(s, terminals.GetValueOrDefault(s.TerminalNo))).ToList();
    }

    public async Task<SalPosSessionDto> CloseAsync(SalCloseSessionRequestDto request,
                                                   CancellationToken ct = default)
    {
        long sessionNo = request.SessionNo;
        decimal countedCash = request.CountedCash;

        var session = await RequireSessionAsync(sessionNo, ct);
        if (session.Status == StClosed) throw new ValidationException("This session is already closed");
        if (countedCash < 0) throw new ValidationException("Counted cash cannot be negative");

        var totals = await TenderTotalsAsync(sessionNo, ct);
        var sales = await SessionSalesAsync(sessionNo, ct);

        decimal expectedCash = totals.GetValueOrDefault(MethodCash);
        decimal variance = countedCash - (session.OpeningFloat + expectedCash);

        // A drawer that does not reconcile is the single most useful fraud signal a till produces,
        // so closing on a discrepancy takes a deliberate sign-off rather than a shrug.
        if (Math.Abs(variance) > VarianceTolerance && !request.VarianceApproved)
        {
            throw new ValidationException(
                $"Drawer is out by {variance:0.00} — a supervisor must approve the variance before closing");
        }

        if (Math.Abs(variance) > VarianceTolerance && string.IsNullOrWhiteSpace(request.Remarks))
            throw new ValidationException("Explain the variance before closing");

        session.ExpectedCash = expectedCash;
        session.ExpectedCard = totals.GetValueOrDefault(MethodCard);
        session.ExpectedMobile = totals.GetValueOrDefault(MethodMobile);
        session.ExpectedOther = totals.Where(t => t.Key != MethodCash && t.Key != MethodCard
                                                  && t.Key != MethodMobile)
                                      .Sum(t => t.Value);
        session.CountedCash = countedCash;
        session.CashVariance = variance;
        session.TotalSales = sales.Total;
        session.InvoiceCount = sales.Count;
        session.Status = StClosed;
        session.ClosedAt = DateTime.UtcNow;
        session.VarianceRemarks = request.Remarks;

        // Who signed off is the point of the record; without it the approval proves nothing.
        if (Math.Abs(variance) > VarianceTolerance) session.VarianceApprovedBy = _ctx.CurrentUserNo();

        session.UpdatedBy = _ctx.CurrentUserNo();
        session.UpdatedAt = DateTime.UtcNow;

        EmitSessionClose(session);
        await _db.SaveChangesAsync(ct);

        var terminal = await _db.SalPosTerminals.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TerminalNo == session.TerminalNo, ct);

        return ToDto(session, terminal);
    }

    public async Task<SalPosSession> RequireOpenSessionAsync(long terminalNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        return await _db.SalPosSessions.FirstOrDefaultAsync(
                   s => s.TerminalNo == terminalNo && s.CompanyNo == companyNo
                        && s.Status == StOpen && s.IsDeleted == Deleted, ct)
               ?? throw new ValidationException("No open session on this terminal — open a session before selling");
    }

    // ── GL hand-off ──────────────────────────────────────────────────────────

    /// <summary>
    /// Banks the drawer: Dr cash-in-transit for what is being taken out, Cr drawer cash for what the
    /// system says was taken, and the difference to over/short. A perfectly counted drawer emits two
    /// legs; a short one emits three.
    /// </summary>
    private void EmitSessionClose(SalPosSession session)
    {
        decimal expectedCash = session.ExpectedCash ?? 0m;
        decimal variance = session.CashVariance ?? 0m;

        if (expectedCash == 0 && variance == 0) return;

        var legs = new List<GlPostingPayload.Leg>();

        if (expectedCash != 0)
        {
            legs.Add(new() { LegKey = "CASH_IN_TRANSIT", Amount = expectedCash + variance, DrCr = "dr" });
            legs.Add(new() { LegKey = "DRAWER_CASH", Amount = expectedCash, DrCr = "cr" });
        }

        if (variance != 0)
        {
            // Over means the drawer holds more than the tenders explain, so it credits income.
            legs.Add(new()
            {
                LegKey = "CASH_OVER_SHORT",
                Amount = Math.Abs(variance),
                DrCr = variance > 0 ? "cr" : "dr"
            });
        }

        var payload = new GlPostingPayload
        {
            VoucherDate = (session.ClosedAt ?? DateTime.UtcNow).Date,
            Narration = $"POS session close {session.SessionId}",
            BranchNo = session.BranchNo,
            Legs = legs
        };

        _db.EventOutboxes.Add(new EventOutbox
        {
            CompanyNo = session.CompanyNo,
            BranchNo = session.BranchNo,
            EventType = "PosSessionClosePosted",
            AggregateType = "SAL_POS_SESSION",
            AggregateId = session.SessionNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        });
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Tender take per method for a session, from the sales actually confirmed on it.</summary>
    private async Task<Dictionary<short, decimal>> TenderTotalsAsync(long sessionNo, CancellationToken ct)
    {
        return await (from p in _db.SalInvoicePayments
                      join i in _db.SalInvoices on p.InvoiceNo equals i.InvoiceNo
                      where i.PosSessionNo == sessionNo && i.IsDeleted == Deleted && i.Status == SaleConfirmed
                      group p by p.PaymentMethod into g
                      select new { Method = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Method, x => x.Amount, ct);
    }

    private async Task<(decimal Total, int Count, decimal Due)> SessionSalesAsync(long sessionNo,
                                                                                  CancellationToken ct)
    {
        var rows = await _db.SalInvoices.AsNoTracking()
            .Where(i => i.PosSessionNo == sessionNo && i.IsDeleted == Deleted && i.Status == SaleConfirmed)
            .Select(i => new { i.GrandTotal, i.DueAmount })
            .ToListAsync(ct);

        return (rows.Sum(r => r.GrandTotal), rows.Count, rows.Sum(r => r.DueAmount));
    }

    private async Task<SalPosTerminal> RequireTerminalAsync(long terminalNo, CancellationToken ct)
    {
        long companyNo = Company(), branchNo = Branch();

        return await _db.SalPosTerminals.AsNoTracking()
                   .FirstOrDefaultAsync(t => t.TerminalNo == terminalNo && t.CompanyNo == companyNo
                                             && t.BranchNo == branchNo && t.IsDeleted == Deleted, ct)
               ?? throw new NotFoundException("Terminal not found");
    }

    private async Task<SalPosSession> RequireSessionAsync(long sessionNo, CancellationToken ct)
    {
        long companyNo = Company();

        return await _db.SalPosSessions.FirstOrDefaultAsync(
                   s => s.SessionNo == sessionNo && s.CompanyNo == companyNo && s.IsDeleted == Deleted, ct)
               ?? throw new NotFoundException("Session not found");
    }

    private static string MethodName(short method) => method switch
    {
        1 => "Cash",
        2 => "Card",
        3 => "Mobile Banking",
        4 => "Bank Transfer",
        5 => "Cheque",
        6 => "Credit / Due",
        7 => "Loyalty Points",
        8 => "Gift Card",
        9 => "Store Credit",
        _ => $"Method {method}"
    };

    private static SalPosSessionDto ToDto(SalPosSession s, SalPosTerminal? terminal) => new()
    {
        SessionNo = s.SessionNo,
        SessionId = s.SessionId,
        TerminalNo = s.TerminalNo,
        TerminalName = terminal?.TerminalName,
        WarehouseNo = terminal?.WarehouseNo ?? 0,
        CashierUserNo = s.CashierUserNo,
        OpenedAt = s.OpenedAt,
        OpeningFloat = s.OpeningFloat,
        ClosedAt = s.ClosedAt,
        ExpectedCash = s.ExpectedCash,
        CountedCash = s.CountedCash,
        CashVariance = s.CashVariance,
        TotalSales = s.TotalSales,
        TotalReturns = s.TotalReturns,
        InvoiceCount = s.InvoiceCount,
        Status = s.Status,
        VarianceRemarks = s.VarianceRemarks
    };

    /// <summary>The session records who opened the drawer, so the cashier must be identifiable.</summary>
    private long CurrentUser()
    {
        long? user = _ctx.CurrentUserNo();
        if (user is null or 0) throw new ValidationException("No user in context");
        return user.Value;
    }

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");
}
