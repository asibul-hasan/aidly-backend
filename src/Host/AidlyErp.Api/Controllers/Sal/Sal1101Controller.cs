using Microsoft.AspNetCore.Mvc;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Services;

namespace AidlyErp.Api.Controllers.Sal;

[Route("api/sal/1101")]
[Route("api/v1/sal/forms/sal1101")]
public class Sal1101Controller : ApiControllerBase
{
    private readonly ISal1101Service _service;
    public Sal1101Controller(ISal1101Service service) => _service = service;

    [HttpGet("customers")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("customers/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("customers")]
    public async Task<IActionResult> Save([FromBody] Sal1101CustomerDto dto) => OkResponse(await _service.SaveAsync(dto), dto.CustomerNo.HasValue ? "Customer updated" : "Customer created");

    [HttpDelete("customers/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Customer deleted");
    }
}
