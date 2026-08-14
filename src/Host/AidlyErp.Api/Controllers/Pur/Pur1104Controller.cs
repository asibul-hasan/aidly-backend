using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1104")]
[Route("api/v1/pur/forms/pur1104")]
public class Pur1104Controller : ApiControllerBase
{
    private readonly IPur1104Service _service;
    private readonly IPur1001Service _supplierService;
    public Pur1104Controller(IPur1104Service service, IPur1001Service supplierService)
    {
        _service = service;
        _supplierService = supplierService;
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers() => OkResponse(await _supplierService.GetListAsync());

    [HttpGet("open-invoices")]
    public async Task<IActionResult> GetOpenInvoices([FromQuery] long supplierNo)
        => OkResponse(await _service.GetOpenInvoicesAsync(supplierNo));

    [HttpGet("payments")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("payments/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("payments")]
    public async Task<IActionResult> Create([FromBody] Pur1104PaymentDto dto)
        => OkResponse(await _service.CreateAsync(dto), "Payment posted");

    [HttpPost("payments/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body)
        => OkResponse(await _service.CancelAsync(id, body?.Reason), "Payment cancelled");
}
