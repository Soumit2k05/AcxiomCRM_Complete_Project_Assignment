using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
    }

    // POST: /api/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ApiLoginResponseDto>>> Login([FromBody] ApiLoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<ApiLoginResponseDto>.Fail("Invalid credentials format.", errors));
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            await _auditService.LogAsync(
                request.Email,
                "Failed Login",
                "API Authentication",
                result: "Failure",
                details: "REST API login attempted with unknown user.",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Unauthorized(ApiResponse<ApiLoginResponseDto>.Fail("Invalid email or password."));
        }

        if (!user.IsActive)
        {
            return StatusCode(403, ApiResponse<ApiLoginResponseDto>.Fail("Account is deactivated."));
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            var roles = await _userManager.GetRolesAsync(user);

            await _auditService.LogAsync(
                user.Email,
                "Login",
                "API Authentication",
                recordId: user.Id,
                result: "Success",
                details: "REST API login successful.",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            var responseDto = new ApiLoginResponseDto
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Role = roles.FirstOrDefault() ?? "SalesExecutive"
            };

            return Ok(ApiResponse<ApiLoginResponseDto>.Ok(responseDto, "Authentication successful."));
        }

        if (result.IsLockedOut)
        {
            await _auditService.LogAsync(
                user.Email,
                "Lockout",
                "API Authentication",
                recordId: user.Id,
                result: "LockedOut",
                details: "REST API login locked account.",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return StatusCode(423, ApiResponse<ApiLoginResponseDto>.Fail("Account is temporarily locked out."));
        }

        await _auditService.LogAsync(
            user.Email,
            "Failed Login",
            "API Authentication",
            recordId: user.Id,
            result: "Failure",
            details: "REST API password mismatch.",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Unauthorized(ApiResponse<ApiLoginResponseDto>.Fail("Invalid email or password."));
    }

    // POST: /api/auth/logout
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<string>>> Logout()
    {
        var userName = User.Identity?.Name;
        await _signInManager.SignOutAsync();

        await _auditService.LogAsync(
            userName,
            "Logout",
            "API Authentication",
            result: "Success",
            details: "REST API user logged out.",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<string>.Ok("Logged out successfully."));
    }
}
