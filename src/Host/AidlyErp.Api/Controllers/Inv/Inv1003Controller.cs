using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/1003")]
[Route("api/v1/inv/forms/inv1003")]
public class Inv1003Controller : ApiControllerBase
{
    private readonly IInv1003Service _service;
    public Inv1003Controller(IInv1003Service service) => _service = service;

    [HttpGet("categories")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpPost("categories")]
    public async Task<IActionResult> Save([FromBody] Inv1003CategoryDto dto) => OkResponse(await _service.SaveAsync(dto), dto.CategoryNo.HasValue ? "Category updated" : "Category saved");

    [HttpDelete("categories/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Category deleted");
    }
}
