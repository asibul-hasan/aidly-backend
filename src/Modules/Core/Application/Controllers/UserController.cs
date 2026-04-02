using Microsoft.AspNetCore.Mvc;
using Aidly.src.Modules.Core.Application.DTOs;
using Aidly.src.Modules.Core.Domain.Interfaces;

namespace Aidly.src.Modules.Core.Application.Controllers;

[ApiController]
[Route("aidly/core/users")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var result = await _userService.GetAllUsersAsync();
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Error);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var result = await _userService.GetUserByIdAsync(id);
        if (result.IsSuccess) return Ok(result.Value);
        return NotFound(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        var result = await _userService.CreateUserAsync(dto);
        if (result.IsSuccess) return CreatedAtAction(nameof(GetUserById), new { id = result.Value?.UserNo }, result.Value);
        return BadRequest(result.Error);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
    {
        var result = await _userService.UpdateUserAsync(id, dto);
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Error);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var result = await _userService.DeleteUserAsync(id);
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Error);
    }
}
