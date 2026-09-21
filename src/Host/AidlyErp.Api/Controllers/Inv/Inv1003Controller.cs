using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/1003")]
[Route("api/v1/inv/forms/inv1003")]
public class Inv1003Controller : ApiControllerBase
{
    private readonly IInv1003Service _service;
    private readonly IInv1001Service _products;

    public Inv1003Controller(IInv1003Service service, IInv1001Service products)
    {
        _service = service;
        _products = products;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    /// <summary>Candidate parents; the form removes the node itself and its descendants.</summary>
    [HttpGet("parent-options")]
    public async Task<IActionResult> GetParentOptions() => OkResponse(await _service.GetParentOptionsAsync());

    /// <summary>VAT codes for the category's default tax — the same list the product form offers.</summary>
    [HttpGet("tax-options")]
    public async Task<IActionResult> GetTaxOptions() => OkResponse((await _products.GetLookupsAsync()).Taxes);

    [HttpPost("categories")]
    public async Task<IActionResult> Save([FromBody] Inv1003CategoryDto dto) => OkResponse(await _service.SaveAsync(dto), dto.CategoryNo.HasValue ? "Category updated" : "Category saved");

    [HttpDelete("categories/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Category deleted");
    }
}
