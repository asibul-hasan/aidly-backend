using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/1001")]
[Route("api/v1/inv/forms/inv1001")]
public class Inv1001Controller : ApiControllerBase
{
    private readonly IInv1001Service _service;
    public Inv1001Controller(IInv1001Service service) => _service = service;

    [HttpGet("products")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("products/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("products")]
    public async Task<IActionResult> Save([FromBody] Inv1001ProductDto dto) => OkResponse(await _service.SaveAsync(dto), dto.ProductNo.HasValue ? "Product updated" : "Product saved");

    [HttpDelete("products/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Product deleted");
    }

    [HttpGet("resolve-barcode")]
    public async Task<IActionResult> ResolveBarcode([FromQuery] string barcode)
    {
        var res = await _service.ResolveBarcodeAsync(barcode);
        return res != null ? OkResponse(res) : NotFound("Barcode not found");
    }
}
