using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SkillSnap.Api.Models;
using SkillSnap.Shared.Models;
using SkillSnap.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;

// Handles registration, login (JWT issuance), role promotion, and account deletion.
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly JwtOptions _jwt;
    private readonly SkillSnapContext _context;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<JwtOptions> jwtOptions,
        SkillSnapContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwt = jwtOptions.Value;
        _context = context;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Check the PortfolioUser table for an existing UserName (case-insensitive)
        if (await _context.PortfolioUsers
            .AnyAsync(pu => pu.UserName.ToLower() == model.UserName.ToLower()))
            return Conflict(new { message = "That username already exists."});

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser { UserName = model.UserName };
            var result = await _userManager.CreateAsync(user, model.Password!);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return BadRequest(ModelState);
            }

            // Assign the default role.
            var roleResult = await _userManager.AddToRoleAsync(user, Roles.User);
            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                await tx.RollbackAsync();
                return BadRequest(ModelState);
            }

            _context.PortfolioUsers.Add(new PortfolioUser
            {
                UserName = model.UserName,
                ApplicationUserId = user.Id
            });
            await _context.SaveChangesAsync();

            await tx.CommitAsync();
            return Ok(new { message = "Registration successful." });
        }
        catch
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { message = "Registration failed. Please try again." });
        }
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByNameAsync(model.UserName!);

        // Same generic response whether user missing or password wrong — avoids user enumeration.
        if (user is null)
            return Unauthorized(new { message = "Invalid username or password." });

        // SignInManager validates the password and enforces lockout, without issuing a cookie.
        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password!, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return Unauthorized(new { message = "Account locked. Try again later." });

        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid username or password." });

        var token = await GenerateJwtTokenAsync(user);
        return Ok(new { token });
    }

    // GET /api/auth/users
    [HttpGet("users")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userManager.Users.ToListAsync();

        // Map ApplicationUserId -> portfolio display name for a friendlier UI.
        var displayNames = await _context.PortfolioUsers
            .AsNoTracking()
            .Where(pu => pu.ApplicationUserId != null)
            .ToDictionaryAsync(pu => pu.ApplicationUserId!, pu => pu.UserName);

        var result = new List<UserSummaryDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserSummaryDto
            {
                UserName = user.UserName!,
                DisplayName = displayNames.TryGetValue(user.Id, out var name) ? name : null,
                Roles = roles.ToList()
            });
        }

        return Ok(result);
    }

    // POST /api/auth/promote
    [HttpPost("promote")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Promote([FromBody] PromoteRequest model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Only allow promotion to the elevated roles.
        if (model.Role != Roles.Manager && model.Role != Roles.Admin)
        {
            return BadRequest(new { message = $"Role must be '{Roles.Manager}' or '{Roles.Admin}'." });
        }

        var user = await _userManager.FindByNameAsync(model.UserName!);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (await _userManager.IsInRoleAsync(user, model.Role))
        {
            return Ok(new { message = $"{model.UserName} is already a {model.Role}." });
        }

        // Ensure the target role exists before assigning it.
        if (!await _roleManager.RoleExistsAsync(model.Role))
            await _roleManager.CreateAsync(new IdentityRole(model.Role));

        var result = await _userManager.AddToRoleAsync(user, model.Role);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return BadRequest(ModelState);
        }

        return Ok(new { message = $"{model.UserName} promoted to {model.Role}." });
    }

    // DELETE /api/auth/me
    [HttpDelete("me")]
    [Authorize]
    public async Task<IActionResult> DeleteMyAccount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(new { message = "Account not found." });

        return await DeleteAccountAsync(user);
    }

    // DELETE /api/auth/users/{userName}
    [HttpDelete("users/{userName}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteUser(string userName)
    {
        var user = await _userManager.FindByNameAsync(userName);
        if (user is null)
            return NotFound(new { message = "User not found." });

        // Admins remove their own account via DELETE /me to avoid accidental lockout.
        if (user.Id == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return BadRequest(new { message = "Use the account settings page to delete your own account." });

        return await DeleteAccountAsync(user);
    }

    // Removes the Identity login and its linked portfolio (Projects/Skills cascade).
    private async Task<IActionResult> DeleteAccountAsync(ApplicationUser user)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var portfolio = await _context.PortfolioUsers
                .FirstOrDefaultAsync(pu => pu.ApplicationUserId == user.Id);
            if (portfolio is not null)
            {
                _context.PortfolioUsers.Remove(portfolio);
                await _context.SaveChangesAsync();
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                await tx.RollbackAsync();
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return BadRequest(ModelState);
            }

            await tx.CommitAsync();
            return NoContent();
        }
        catch
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { message = "Account deletion failed. Please try again." });
        }
    }

    private async Task<string> GenerateJwtTokenAsync(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!)
        };

        // Include roles as claims so [Authorize(Roles = "...")] works.
        foreach (var role in await _userManager.GetRolesAsync(user))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
