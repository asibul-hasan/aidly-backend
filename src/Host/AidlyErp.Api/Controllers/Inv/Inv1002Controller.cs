using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/1002")]
[Route("api/v1/inv/forms/inv1002")]
public class Inv1002Controller : ApiControllerBase
{
    private readonly IInv1002Service _service;
    public Inv1002Controller(IInv1002Service service) => _service = service;

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _service.GetLookupsAsync());

    [HttpGet("barcodes")]
    public async Task<IActionResult> GetBarcodes([FromQuery] long productNo)
        => OkResponse(await _service.GetBarcodesAsync(productNo));

    /// <summary>A fresh internal EAN-13 for the "generate" button — not yet persisted.</summary>
    [HttpGet("barcodes/next-code")]
    public async Task<IActionResult> NextCode()
        => OkResponse(new { code = await _service.NextCodeAsync() });

    [HttpPost("barcodes")]
    public async Task<IActionResult> Save([FromBody] Inv1002BarcodeDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.BarcodeNo.HasValue ? "Barcode updated" : "Barcode saved");

    [HttpDelete("barcodes/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Barcode deleted");
    }
}
