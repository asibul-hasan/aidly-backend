using Microsoft.AspNetCore.Mvc;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1002")]
[Route("api/v1/pur/forms/pur1002")]
public class Pur1002Controller : ApiControllerBase
{
    private readonly IPur1002Service _service;
    public Pur1002Controller(IPur1002Service service) => _service = service;

    [HttpGet("suppliers/{supplierNo:long}/products")]
    public async Task<IActionResult> GetRows(long supplierNo) => OkResponse(await _service.GetRowsAsync(supplierNo));

    [HttpPost("suppliers/{supplierNo:long}/products")]
    public async Task<IActionResult> SaveRows(long supplierNo, [FromBody] List<Pur1002PriceRowDto> rows)
        => OkResponse(await _service.SaveRowsAsync(supplierNo, rows), "Price list saved");

    [HttpDelete("products/{id:long}")]
    public async Task<IActionResult> DeleteRow(long id)
    {
        await _service.DeleteRowAsync(id);
        return OkResponse<object?>(null, "Price row deleted");
    }
}
