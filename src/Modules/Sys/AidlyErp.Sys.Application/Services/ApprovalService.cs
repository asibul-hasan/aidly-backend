using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Sys.Application.Services;

/// <summary>
/// Approval engine — full port of the Java <c>sys.service.ApprovalService</c>.
///
/// <para>A document is routed through the most specific configured scope for the current
/// company/menu/branch/department. When no scope with steps applies, the document auto-approves
/// rather than blocking.</para>
/// </summary>
public class ApprovalService : IApprovalService
{
    private const short Deleted = 0;

    private const short ReqPending = 1, ReqApproved = 2, ReqRejected = 3, ReqCancelled = 4;
    private const short StepPending = 1, StepApproved = 2, StepRejected = 3;
    private const short TypeForward = 1, TypeBackward = 2, TypeReject = 3;

    /// <summary>Exposed for callers filtering by request status.</summary>
    public const short StatusPending = ReqPending, StatusApproved = ReqApproved, StatusRejected = ReqRejected;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ICurrentPermissionContext _permissionContext;
    private readonly IEnumerable<IApprovalCompletedListener> _listeners;
    private readonly INotificationDispatcher? _notificationDispatcher;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                           ICurrentPermissionContext permissionContext,
                           IEnumerable<IApprovalCompletedListener> listeners,
                           ILogger<ApprovalService> logger,
                           INotificationDispatcher? notificationDispatcher = null)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _permissionContext = permissionContext;
        _listeners = listeners;
        _logger = logger;
        _notificationDispatcher = notificationDispatcher;
    }

    // ─── Raise ───────────────────────────────────────────────────────────────

    public Task<ApprovalOutcome> RaiseAsync(string documentType, long documentPk, string? documentNo,
                                            decimal? amount, CancellationToken cancellationToken = default) =>
        RaiseAsync(documentType, documentPk, documentNo, amount, null, cancellationToken);

    public async Task<ApprovalOutcome> RaiseAsync(string documentType, long documentPk, string? documentNo,
                                                  decimal? amount, long? departmentNo,
                                                  CancellationToken cancellationToken = default)
    {
        var menuNo = await ResolveCurrentMenuNoAsync(documentType, cancellationToken);
        return await RaiseCoreAsync(menuNo, documentType, documentPk, documentNo, amount, departmentNo,
            cancellationToken);
    }

    public async Task<ApprovalOutcome> RaiseOnFormAsync(string? menuFormId, string documentType, long documentPk,
                                                        string? documentNo, decimal? amount, long? departmentNo,
                                                        CancellationToken cancellationToken = default)
    {
        var menuNo = string.IsNullOrWhiteSpace(menuFormId)
            ? null
            : await MenuNoForFormAsync(menuFormId, cancellationToken);

        return await RaiseCoreAsync(menuNo, documentType, documentPk, documentNo, amount, departmentNo,
            cancellationToken);
    }

    public async Task<ApprovalOutcome> RaiseOnFirstConfiguredFormAsync(IReadOnlyList<string>? menuFormIds,
                                                                       string documentType, long documentPk,
                                                                       string? documentNo, decimal? amount,
                                                                       long? departmentNo,
                                                                       CancellationToken cancellationToken = default)
    {
        var companyNo = RequireCompany();

        if (menuFormIds != null)
        {
            foreach (var formId in menuFormIds)
            {
                if (string.IsNullOrWhiteSpace(formId)) continue;

                var menuNo = await MenuNoForFormAsync(formId, cancellationToken);
                if (menuNo == null) continue;

                var hasActiveScope = await _db.ApprovalScopes
                    .AsNoTracking()
                    .AnyAsync(s => s.CompanyNo == companyNo && s.MenuNo == menuNo && s.IsDeleted == Deleted
                                   && s.IsActive == 1, cancellationToken);

                if (hasActiveScope)
                {
                    return await RaiseCoreAsync(menuNo, documentType, documentPk, documentNo, amount, departmentNo,
                        cancellationToken);
                }
            }
        }

        _logger.LogInformation(
            "Approval auto-approved (no workflow configured on any candidate form): documentType={DocumentType}, pk={Pk}, forms={Forms}",
            documentType, documentPk, menuFormIds == null ? "-" : string.Join(",", menuFormIds));

        return ApprovalOutcome.Auto();
    }

    private Task<ApprovalOutcome> RaiseCoreAsync(long? menuNo, string documentType, long documentPk,
                                                 string? documentNo, decimal? amount, long? departmentNo,
                                                 CancellationToken cancellationToken) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var companyNo = RequireCompany();
            var branchNo = _ctx.BranchNo;
            var amt = amount ?? 0m;

            if (menuNo == null)
            {
                _logger.LogInformation(
                    "Approval auto-approved (no menu resolved for the current form): documentType={DocumentType}, pk={Pk}",
                    documentType, documentPk);
                return ApprovalOutcome.Auto();
            }

            var active = await _db.ApprovalScopes
                .AsNoTracking()
                .Where(s => s.CompanyNo == companyNo && s.MenuNo == menuNo && s.IsDeleted == Deleted
                            && s.IsActive == 1)
                .ToListAsync(ct);

            // Scopes matching this branch/department come first, most specific first; the rest
            // follow least-specific first as a fallback.
            var candidates = new List<ApprovalScope>();

            candidates.AddRange(active
                .Where(s => Matches(s.BranchNo, branchNo) && Matches(s.DepartmentNo, departmentNo))
                .OrderByDescending(Specificity));

            candidates.AddRange(active
                .Where(s => !(Matches(s.BranchNo, branchNo) && Matches(s.DepartmentNo, departmentNo)))
                .OrderBy(Specificity));

            ApprovalScope? scope = null;
            var steps = new List<ApprovalStep>();

            // The first candidate that actually has steps wins — a scope with none is skipped.
            foreach (var cand in candidates)
            {
                var candSteps = await _db.ApprovalSteps
                    .AsNoTracking()
                    .Where(s => s.ScopeNo == cand.ScopeNo && s.IsActive == 1 && s.IsDeleted == Deleted)
                    .OrderBy(s => s.StepNumber)
                    .ToListAsync(ct);

                if (candSteps.Count > 0)
                {
                    scope = cand;
                    steps = candSteps;
                    break;
                }
            }

            if (scope == null)
            {
                _logger.LogInformation(
                    "Approval auto-approved (no workflow with steps configured for menu): documentType={DocumentType}, menuNo={MenuNo}, pk={Pk}",
                    documentType, menuNo, documentPk);
                return ApprovalOutcome.Auto();
            }

            var req = new ApprovalRequest
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                ScopeNo = scope.ScopeNo,
                DocumentType = documentType,
                DocumentNo = documentNo ?? documentPk.ToString(),
                DocumentPk = documentPk,
                Amount = amt,
                CurrentStep = steps[0].StepNumber,
                Status = ReqPending,
                RequestedBy = _ctx.CurrentUserNo()
            };

            _db.ApprovalRequests.Add(req);
            await _db.SaveChangesAsync(ct);

            // One row per (step, configured approver) — the whole chain is materialised up front.
            var stepNos = steps.Select(s => s.StepNo).ToList();

            var approversByStep = (await _db.StepApprovers
                    .AsNoTracking()
                    .Where(a => stepNos.Contains(a.StepNo))
                    .ToListAsync(ct))
                .GroupBy(a => a.StepNo)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var w in steps)
            {
                if (!approversByStep.TryGetValue(w.StepNo, out var approvers)) continue;

                foreach (var approver in approvers)
                {
                    _db.ApprovalRequestSteps.Add(new ApprovalRequestStep
                    {
                        ApprovalRequestNo = req.ApprovalRequestNo,
                        StepNumber = w.StepNumber,
                        EmpNo = approver.EmpNo,
                        Action = StepPending
                    });
                }
            }

            await _db.SaveChangesAsync(ct);

            // Tell the first step's approvers there is something waiting. Staged in THIS
            // transaction, so an approval request and its notifications commit together.
            await NotifyStepApproversAsync(req, req.CurrentStep, menuNo,
                NotificationEvents.ApprovalRequested,
                "Approval required",
                $"{await DescribeAsync(req, ct)} is awaiting your approval.",
                ct);

            _logger.LogInformation("Approval raised: requestNo={RequestNo}, documentType={DocumentType}, pk={Pk}, steps={Steps}",
                req.ApprovalRequestNo, documentType, documentPk, steps.Count);

            return ApprovalOutcome.Pending(req.ApprovalRequestNo);
        }, cancellationToken);

    // ─── Act ─────────────────────────────────────────────────────────────────

    public Task ActAsync(long approvalRequestNo, bool approve, string? remarks,
                         CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var req = await _db.ApprovalRequests
                          .FirstOrDefaultAsync(r => r.ApprovalRequestNo == approvalRequestNo, ct)
                      ?? throw new NotFoundException("Approval request not found: " + approvalRequestNo);

            if (req.Status != ReqPending)
            {
                throw new ValidationException("Approval request is not pending");
            }

            var userNo = _ctx.CurrentUserNo();
            var empNo = await CurrentEmployeeNoAsync(ct);

            var allSteps = await _db.ApprovalRequestSteps
                .Where(s => s.ApprovalRequestNo == approvalRequestNo)
                .OrderBy(s => s.StepNumber)
                .ToListAsync(ct);

            // The caller must be an approver on the step the request is currently sitting at.
            var step = allSteps.FirstOrDefault(s => s.StepNumber == req.CurrentStep && empNo != null && s.EmpNo == empNo)
                       ?? throw new ValidationException(
                           "You are not an approver for the current step of this request");

            step.Action = approve ? StepApproved : StepRejected;
            step.ActedBy = userNo;
            step.ActedAt = DateTime.UtcNow;
            step.Remarks = remarks;

            if (!approve)
            {
                await CompleteAsync(req, ReqRejected, false, ct);
                return;
            }

            var config = await CurrentStepConfigAsync(req, ct);
            var stepType = config?.StepType ?? TypeForward;

            if (stepType == TypeReject)
            {
                await CompleteAsync(req, ReqRejected, false, ct);
                return;
            }

            if (config?.IsFinal == 1)
            {
                await CompleteAsync(req, ReqApproved, true, ct);
                return;
            }

            // An explicit next step wins; otherwise walk forward (or backward) to the nearest
            // configured step number.
            short? target = config?.NextStepNo;

            if (target == null)
            {
                var distinct = allSteps.Select(s => s.StepNumber).Distinct().ToList();

                var forward = distinct.Where(sn => sn > req.CurrentStep).ToList();
                var backward = distinct.Where(sn => sn < req.CurrentStep).ToList();

                target = stepType == TypeBackward
                    ? backward.Count > 0 ? backward.Max() : (short?)null
                    : forward.Count > 0 ? forward.Min() : (short?)null;
            }

            if (target == null)
            {
                // Nothing left to route to ⇒ fully approved.
                await CompleteAsync(req, ReqApproved, true, ct);
                return;
            }

            // Re-open the target step so its approvers can act (matters when routing backward).
            foreach (var s in allSteps.Where(s => s.StepNumber == target))
            {
                s.Action = StepPending;
                s.ActedBy = null;
                s.ActedAt = null;
            }

            req.CurrentStep = target.Value;
            await _db.SaveChangesAsync(ct);

            // The queue moved on — the next step's approvers are the ones who now need to act.
            await NotifyStepApproversAsync(req, req.CurrentStep, null,
                NotificationEvents.ApprovalStepAdvanced,
                "Approval required",
                $"{await DescribeAsync(req, ct)} has advanced to your approval step.",
                ct);

            _logger.LogInformation("Approval routed: requestNo={RequestNo}, from step={From} type={Type} -> step={To}",
                approvalRequestNo, step.StepNumber, stepType, target);
        }, cancellationToken);

    public Task CancelRequestAsync(long? approvalRequestNo, CancellationToken cancellationToken = default) =>
        approvalRequestNo == null
            ? Task.CompletedTask
            : _unitOfWork.ExecuteAsync(async ct =>
            {
                var req = await _db.ApprovalRequests
                    .FirstOrDefaultAsync(r => r.ApprovalRequestNo == approvalRequestNo, ct);

                // Silently ignores a missing or already-settled request, as in Java.
                if (req == null || req.Status != ReqPending) return;

                req.Status = ReqCancelled;
                req.CompletedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Approval request cancelled: requestNo={RequestNo}", approvalRequestNo);
            }, cancellationToken);

    // ─── Query ───────────────────────────────────────────────────────────────

    public async Task<List<PendingApprovalDto>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = RequireCompany();
        var empNo = await CurrentEmployeeNoAsync(cancellationToken);
        if (empNo == null) return new List<PendingApprovalDto>();

        var requests = await _db.ApprovalRequests
            .AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.Status == ReqPending)
            .OrderByDescending(r => r.ApprovalRequestNo)
            .ToListAsync(cancellationToken);

        if (requests.Count == 0) return new List<PendingApprovalDto>();

        var requestNos = requests.Select(r => r.ApprovalRequestNo).ToList();

        // Only steps assigned to this approver — one query, then matched per request.
        var mySteps = await _db.ApprovalRequestSteps
            .AsNoTracking()
            .Where(s => requestNos.Contains(s.ApprovalRequestNo) && s.EmpNo == empNo)
            .ToListAsync(cancellationToken);

        var result = new List<PendingApprovalDto>();

        foreach (var req in requests)
        {
            var step = mySteps.FirstOrDefault(s => s.ApprovalRequestNo == req.ApprovalRequestNo
                                                   && s.StepNumber == req.CurrentStep);

            if (step != null) result.Add(ToPendingDto(req, step));
        }

        return result;
    }

    public async Task<List<long>> GetPendingDocumentPksForCurrentApproverAsync(string documentType,
                                                                               CancellationToken cancellationToken = default)
    {
        var empNo = await CurrentEmployeeNoAsync(cancellationToken);
        if (empNo == null) return new List<long>();

        return await (from r in _db.ApprovalRequests.AsNoTracking().IgnoreQueryFilters()
                      join s in _db.ApprovalRequestSteps.AsNoTracking().IgnoreQueryFilters()
                          on r.ApprovalRequestNo equals s.ApprovalRequestNo
                      where (r.DocumentType == documentType || r.DocumentType == "HRM_1301" || r.DocumentType == "HRM_1321")
                            && r.Status == ReqPending
                            && s.EmpNo == empNo && s.StepNumber == r.CurrentStep
                            && r.DocumentPk != null
                      select r.DocumentPk!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<long>> GetInvolvedDocumentPksAsync(string documentType, short requestStatus,
                                                              CancellationToken cancellationToken = default)
    {
        var empNo = await CurrentEmployeeNoAsync(cancellationToken);
        if (empNo == null) return new List<long>();

        return await (from r in _db.ApprovalRequests.AsNoTracking().IgnoreQueryFilters()
                      join s in _db.ApprovalRequestSteps.AsNoTracking().IgnoreQueryFilters()
                          on r.ApprovalRequestNo equals s.ApprovalRequestNo
                      where (r.DocumentType == documentType || r.DocumentType == "HRM_1301" || r.DocumentType == "HRM_1321")
                            && r.Status == requestStatus
                            && s.EmpNo == empNo
                            && r.DocumentPk != null
                            && r.CurrentStep >= s.StepNumber
                      select r.DocumentPk!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<long, ApproverView>> GetApproverViewsAsync(string documentType,
                                                                            CancellationToken cancellationToken = default)
    {
        var empNo = await CurrentEmployeeNoAsync(cancellationToken);
        if (empNo == null) return new Dictionary<long, ApproverView>();

        var companyNo = RequireCompany();

        var rows = await (from r in _db.ApprovalRequests.AsNoTracking().IgnoreQueryFilters()
                          join s in _db.ApprovalRequestSteps.AsNoTracking().IgnoreQueryFilters()
                              on r.ApprovalRequestNo equals s.ApprovalRequestNo
                          where (r.DocumentType == documentType || r.DocumentType == "HRM_1301" || r.DocumentType == "HRM_1321")
                                && r.Status == ReqPending
                                && s.EmpNo == empNo
                                && r.DocumentPk != null
                          select new
                          {
                              Pk = r.DocumentPk!.Value,
                              MyStep = s.StepNumber,
                              CurStep = r.CurrentStep,
                              MyAction = (short?)s.Action
                          })
            .ToListAsync(cancellationToken);

        var views = new Dictionary<long, ApproverView>();

        foreach (var r in rows)
        {
            ApproverView v;

            if (r.MyStep == r.CurStep && (r.MyAction == null || r.MyAction == StepPending))
            {
                v = ApproverView.Actionable;
            }
            else if (r.MyStep > r.CurStep)
            {
                v = ApproverView.Waiting;
            }
            else
            {
                v = ApproverView.Passed;
            }

            // A document can carry several of the approver's steps — keep the most actionable.
            views[r.Pk] = views.TryGetValue(r.Pk, out var existing) && existing <= v ? existing : v;
        }

        return views;
    }

    public async Task<Dictionary<long, string>> GetStepNamesAsync(string documentType, IEnumerable<long> documentPks,
                                                                 CancellationToken cancellationToken = default)
    {
        var pkList = documentPks.Distinct().ToList();
        if (pkList.Count == 0) return new Dictionary<long, string>();

        var requests = await _db.ApprovalRequests.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => (r.DocumentType == documentType || r.DocumentType == "HRM_1321" || r.DocumentType == "HRM_1301")
                        && r.DocumentPk != null && pkList.Contains(r.DocumentPk.Value))
            .ToListAsync(cancellationToken);

        var requestByPk = requests.GroupBy(r => r.DocumentPk!.Value).ToDictionary(g => g.Key, g => g.First());
        var scopeNos = requests.Where(r => r.ScopeNo.HasValue).Select(r => r.ScopeNo!.Value).Distinct().ToList();

        var menuNos = await _db.Menus.AsNoTracking()
            .Where(m => m.FormId == documentType || m.FormId == "HRM_1321" || m.FormId == "HRM_1301")
            .Select(m => m.MenuNo)
            .ToListAsync(cancellationToken);

        var activeScopeNos = await _db.ApprovalScopes.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => menuNos.Contains(s.MenuNo) && s.IsActive == 1 && s.IsDeleted == 0)
            .Select(s => s.ScopeNo)
            .ToListAsync(cancellationToken);

        var allScopeNos = scopeNos.Concat(activeScopeNos).Distinct().ToList();

        var steps = await _db.ApprovalSteps.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => (allScopeNos.Contains(s.ScopeNo) || allScopeNos.Count == 0) && s.IsDeleted == 0)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(cancellationToken);

        var stepDict = steps
            .GroupBy(s => (s.ScopeNo, (int)s.StepNumber))
            .ToDictionary(g => g.Key, g => g.First().StepName ?? $"Step {g.Key.Item2}");

        var step0Name = steps
            .Where(s => s.StepNumber == 0)
            .Select(s => s.StepName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? steps.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.StepName))?.StepName
            ?? "Submit For Approval";

        var result = new Dictionary<long, string>();
        foreach (var pk in pkList)
        {
            if (requestByPk.TryGetValue(pk, out var r))
            {
                if (r.Status == ReqApproved)
                {
                    result[pk] = "Approved";
                }
                else if (r.Status == ReqRejected)
                {
                    result[pk] = "Rejected";
                }
                else if (r.Status == ReqCancelled)
                {
                    result[pk] = "Cancelled";
                }
                else if (r.ScopeNo.HasValue && stepDict.TryGetValue((r.ScopeNo.Value, (int)r.CurrentStep), out var stepName) && !string.IsNullOrWhiteSpace(stepName))
                {
                    result[pk] = stepName;
                }
                else
                {
                    result[pk] = step0Name;
                }
            }
            else
            {
                result[pk] = step0Name;
            }
        }

        return result;
    }

    public async Task<bool> IsUntouchedAsync(long? approvalRequestNo, CancellationToken cancellationToken = default)
    {
        if (approvalRequestNo == null) return true;

        var req = await _db.ApprovalRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ApprovalRequestNo == approvalRequestNo, cancellationToken);

        if (req == null) return true;
        if (req.Status != ReqPending) return false;

        return await _db.ApprovalRequestSteps
            .AsNoTracking()
            .Where(s => s.ApprovalRequestNo == approvalRequestNo)
            .AllAsync(s => s.Action == null || s.Action == StepPending, cancellationToken);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task CompleteAsync(ApprovalRequest req, short status, bool approved, CancellationToken ct)
    {
        req.Status = status;
        req.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Approval {Outcome}: requestNo={RequestNo}, documentType={DocumentType}, pk={Pk}",
            approved ? "APPROVED" : "REJECTED", req.ApprovalRequestNo, req.DocumentType, req.DocumentPk);

        // The chain is settled — tell the person who raised it how it ended.
        await NotifyRequesterAsync(req, approved, ct);

        // Spring publishes ApprovalCompletedEvent here; the .NET equivalent notifies every
        // registered listener so the owning module can apply the outcome to its document.
        if (req.DocumentType == null || req.DocumentPk == null) return;

        foreach (var listener in _listeners)
        {
            await listener.OnApprovalCompletedAsync(req.DocumentType, req.DocumentPk.Value, approved, ct);
        }
    }

    // ── Notifications ─────────────────────────────────────────────────────────
    //
    // Dispatch lives in the engine rather than in each document service. The engine is the only
    // place that knows who the current approvers are, so putting it here means every
    // approval-gated document — leave, payroll, PO, stock adjustment — gets the same
    // notifications for free, and a new module cannot forget to send them.

    /// <summary>
    /// Notifies the approvers sitting on <paramref name="stepNumber"/>.
    ///
    /// <para>Approvers are recorded as employees, while notifications address users, so the
    /// employee numbers are mapped to logins in ONE batched query — never one lookup per approver.
    /// An approver with no user account is skipped: there is no inbox to deliver to.</para>
    /// </summary>
    private async Task NotifyStepApproversAsync(ApprovalRequest req, short stepNumber, long? menuNo,
                                                string triggerEvent, string title, string message,
                                                CancellationToken ct)
    {
        if (_notificationDispatcher == null) return;

        var employeeNos = await _db.ApprovalRequestSteps
            .AsNoTracking()
            .Where(s => s.ApprovalRequestNo == req.ApprovalRequestNo && s.StepNumber == stepNumber && s.EmpNo != null)
            .Select(s => s.EmpNo!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (employeeNos.Count == 0) return;

        var recipients = await _db.Users
            .AsNoTracking()
            .Where(u => u.EmployeeNo != null && employeeNos.Contains(u.EmployeeNo.Value) && u.IsDeleted == Deleted && u.IsActive == 1)
            .Select(u => new { u.UserNo, u.EmployeeNo })
            .ToListAsync(ct);

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Approval requestNo={RequestNo} step={Step} has {Count} approver employee(s) but none has an active user account — nobody can be notified.",
                req.ApprovalRequestNo, stepNumber, employeeNos.Count);
            return;
        }

        var senderUserNo = _ctx.UserNo;

        await _notificationDispatcher.DispatchManyAsync(recipients.Select(r => new NotificationDispatchPayload(
            CompanyNo: req.CompanyNo,
            BranchNo: req.BranchNo,
            TargetUserNo: r.UserNo,
            TargetEmployeeNo: r.EmployeeNo,
            SenderUserNo: senderUserNo,
            MenuNo: menuNo,
            FormId: _permissionContext.FormId,
            DocumentType: req.DocumentType,
            DocumentPk: req.DocumentPk,
            ApprovalRequestNo: req.ApprovalRequestNo,
            Title: title,
            Message: message,
            PayloadData: new
            {
                trigger_event = triggerEvent,
                step_number = stepNumber,
                document_no = req.DocumentNo
            }
        )), ct);
    }

    /// <summary>Tells whoever raised the request how it was decided.</summary>
    private async Task NotifyRequesterAsync(ApprovalRequest req, bool approved, CancellationToken ct)
    {
        if (_notificationDispatcher == null || req.RequestedBy == 0) return;

        // Don't notify someone about their own action — a self-approver already knows.
        if (req.RequestedBy == _ctx.UserNo) return;

        var outcome = approved ? "approved" : "rejected";

        await _notificationDispatcher.DispatchAsync(new NotificationDispatchPayload(
            CompanyNo: req.CompanyNo,
            BranchNo: req.BranchNo,
            TargetUserNo: req.RequestedBy,
            TargetEmployeeNo: null,
            SenderUserNo: _ctx.UserNo,
            MenuNo: null,
            FormId: _permissionContext.FormId,
            DocumentType: req.DocumentType,
            DocumentPk: req.DocumentPk,
            ApprovalRequestNo: req.ApprovalRequestNo,
            Title: approved ? "Approved" : "Rejected",
            Message: $"{await DescribeAsync(req, ct)} was {outcome}.",
            PayloadData: new
            {
                trigger_event = approved ? NotificationEvents.ApprovalApproved : NotificationEvents.ApprovalRejected,
                document_no = req.DocumentNo
            }
        ), ct);
    }

    /// <summary>
    /// Human-readable handle for a request, e.g. "Leave Application LV-16".
    ///
    /// <para><b>Never shows the document type code.</b> "HRM_1301" is an internal form id; the
    /// person reading the notification has no idea what it means. The form NAME comes from
    /// <c>sys_menu</c>, so it is whatever the menu calls the form — and it stays correct if the
    /// form is renamed.</para>
    /// </summary>
    private async Task<string> DescribeAsync(ApprovalRequest req, CancellationToken ct)
    {
        var formName = await ResolveFormNameAsync(req.DocumentType, ct);

        if (string.IsNullOrWhiteSpace(req.DocumentNo))
            return string.IsNullOrWhiteSpace(formName) ? $"#{req.DocumentPk}" : $"{formName} #{req.DocumentPk}";

        return string.IsNullOrWhiteSpace(formName) ? req.DocumentNo : $"{formName} {req.DocumentNo}";
    }

    /// <summary>
    /// Form id → form name, memoized for the lifetime of the request. Returns null when the type
    /// maps to no menu, so the caller degrades to the document number alone rather than printing
    /// a code at the user.
    /// </summary>
    private async Task<string?> ResolveFormNameAsync(string? formId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(formId)) return null;
        if (_formNameCache.TryGetValue(formId, out var cached)) return cached;

        var name = await _db.Menus
            .AsNoTracking()
            .Where(m => m.FormId == formId && m.IsDeleted == Deleted)
            .Select(m => m.FormName)
            .FirstOrDefaultAsync(ct);

        _formNameCache[formId] = name;
        return name;
    }

    private readonly Dictionary<string, string?> _formNameCache = [];

    private async Task<ApprovalStep?> CurrentStepConfigAsync(ApprovalRequest req, CancellationToken ct)
    {
        if (req.ScopeNo == null) return null;

        return await _db.ApprovalSteps
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ScopeNo == req.ScopeNo && s.StepNumber == req.CurrentStep, ct);
    }

    /// <summary>Approvers are employees, so the acting user is resolved to their employee number.</summary>
    private async Task<long?> CurrentEmployeeNoAsync(CancellationToken ct)
    {
        var userNo = _ctx.UserNo;
        if (userNo == null) return null;

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.UserNo == userNo && u.IsDeleted == Deleted)
            .Select(u => u.EmployeeNo)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Prefers the form the request is actually on (from the RBAC permission context), falling
    /// back to treating the document type as a form id.
    /// </summary>
    private async Task<long?> ResolveCurrentMenuNoAsync(string documentType, CancellationToken ct)
    {
        var formId = _permissionContext.FormId;

        if (!string.IsNullOrWhiteSpace(formId))
        {
            var menuNo = await MenuNoForFormAsync(formId, ct);
            if (menuNo != null) return menuNo;
        }

        return string.IsNullOrWhiteSpace(documentType) ? null : await MenuNoForFormAsync(documentType, ct);
    }

    private async Task<long?> MenuNoForFormAsync(string formId, CancellationToken ct) =>
        await _db.Menus
            .AsNoTracking()
            .Where(m => m.FormId == formId && m.IsDeleted == Deleted)
            .Select(m => (long?)m.MenuNo)
            .FirstOrDefaultAsync(ct);

    private long RequireCompany() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    /// <summary>A null configured value is a wildcard — it matches any actual value.</summary>
    private static bool Matches(long? configured, long? actual) => configured == null || configured == actual;

    /// <summary>Department beats branch when ranking how tightly a scope targets the document.</summary>
    private static int Specificity(ApprovalScope s) =>
        (s.DepartmentNo != null ? 4 : 0) + (s.BranchNo != null ? 2 : 0);

    private static PendingApprovalDto ToPendingDto(ApprovalRequest req, ApprovalRequestStep step) => new()
    {
        ApprovalRequestNo = req.ApprovalRequestNo,
        DocumentType = req.DocumentType,
        DocumentNo = req.DocumentNo,
        DocumentPk = req.DocumentPk,
        Amount = req.Amount,
        CurrentStep = req.CurrentStep,
        ApproverRoleNo = step.EmpNo,
        RequestedBy = req.RequestedBy,
        RequestedAt = req.RequestedAt
    };
}
