using Microsoft.AspNetCore.Mvc;
using Aidly.src.Modules.Core.Application.DTOs.Company;
using Aidly.src.Modules.Core.Domain.Interfaces.Company;

namespace Aidly.src.Modules.Core.Application.Controllers.Company;

[ApiController]
[Route("aidly/core/companies")]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompanyController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCompanies()
    {
        var result = await _companyService.GetAllCompaniesAsync();
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Error);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCompanyById(int id)
    {
        var result = await _companyService.GetCompanyByIdAsync(id);
        if (result.IsSuccess) return Ok(result.Value);
        return NotFound(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyDto dto)
    {
        var result = await _companyService.CreateCompanyAsync(dto);
        if (result.IsSuccess) return CreatedAtAction(nameof(GetCompanyById), new { id = result.Value!.CompanyNo }, result.Value);
        return BadRequest(result.Error);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCompany(int id, [FromBody] UpdateCompanyDto dto)
    {
        var result = await _companyService.UpdateCompanyAsync(id, dto);
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Error);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCompany(int id)
    {
        var result = await _companyService.DeleteCompanyAsync(id);
        if (result.IsSuccess) return Ok(result.Value);
        return NotFound(result.Error);
    }
}