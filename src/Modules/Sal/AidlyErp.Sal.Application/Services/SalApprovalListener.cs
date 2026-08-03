namespace AidlyErp.Sal.Application.Services;

public interface ISalApprovalListener
{
    Task OnApprovalOutcomeAsync(long invoiceNo, bool approved, CancellationToken ct = default);
}

public class SalApprovalListener : ISalApprovalListener
{
    private readonly ISal1001Service _invoiceService;

    public SalApprovalListener(ISal1001Service invoiceService)
    {
        _invoiceService = invoiceService;
    }

    public async Task OnApprovalOutcomeAsync(long invoiceNo, bool approved, CancellationToken ct = default)
    {
        await _invoiceService.ApplyApprovalOutcomeAsync(invoiceNo, approved, ct);
    }
}
