using Microsoft.AspNetCore.Mvc;
using AidlyErp.Application.Pur.Dto;
using AidlyErp.Application.Pur.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1001")]
[Route("api/v1/pur/forms/pur1001")]
public class Pur1001Controller : ApiControllerBase
{
    private readonly IPur1001Service _service;
    public Pur1001Controller(IPur1001Service service) => _service = service;

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("suppliers/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("suppliers")]
    public async Task<IActionResult> Save([FromBody] Pur1001SupplierDto dto) => OkResponse(await _service.SaveAsync(dto), dto.SupplierNo.HasValue ? "Supplier updated" : "Supplier created");

    [HttpDelete("suppliers/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Supplier deleted");
    }
}
