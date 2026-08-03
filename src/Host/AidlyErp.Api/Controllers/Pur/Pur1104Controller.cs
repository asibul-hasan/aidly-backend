using Microsoft.AspNetCore.Mvc;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1104")]
[Route("api/v1/pur/forms/pur1104")]
public class Pur1104Controller : ApiControllerBase
{
    private readonly IPur1104Service _service;
    public Pur1104Controller(IPur1104Service service) => _service = service;

    [HttpGet("payments")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpPost("payments")]
    public async Task<IActionResult> Save([FromBody] Pur1104PaymentDto dto) => OkResponse(await _service.SaveAsync(dto), dto.PaymentNo.HasValue ? "Payment updated" : "Payment saved");

    [HttpPost("payments/{id:long}/post")]
    public async Task<IActionResult> Post(long id) => OkResponse(await _service.PostAsync(id), "Payment posted");
}
