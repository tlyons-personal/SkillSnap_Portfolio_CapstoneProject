using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SkillSnap.Shared.Models;
using SkillSnap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SkillSnap.Api.Controllers
{
    // CRUD for portfolio profiles; users manage their own, Admins manage any.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PortfolioUserController : ApiControllerBase
    {
        private readonly SkillSnapContext _context;
        private readonly ILogger<PortfolioUserController> _logger;

        public PortfolioUserController(SkillSnapContext context, ILogger<PortfolioUserController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/PortfolioUser
        [HttpGet]
        public async Task<IActionResult> GetPortfolioUsers()
        {
            var users = await _context.PortfolioUsers
                .AsNoTracking()
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/PortfolioUser/me
        [HttpGet("me")]
        public async Task<IActionResult> GetMyPortfolioUser()
        {
            if (string.IsNullOrEmpty(CurrentUserId))
                return Unauthorized();

            var user = await _context.PortfolioUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(pu => pu.ApplicationUserId == CurrentUserId);

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        // PUT: api/PortfolioUser/me
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyPortfolioUser(PortfolioUser updatedUser)
        {
            if (string.IsNullOrEmpty(CurrentUserId))
                return Unauthorized();

            var user = await _context.PortfolioUsers
                .FirstOrDefaultAsync(pu => pu.ApplicationUserId == CurrentUserId);

            if (user == null)
                return NotFound();

            user.FirstName = updatedUser.FirstName;
            user.LastName = updatedUser.LastName;
            user.Bio = updatedUser.Bio;
            user.ProfileImageUrl = updatedUser.ProfileImageUrl;
            // NOTE: UserName and ApplicationUserId are intentionally left unchanged.

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated their own portfolio {PortfolioUserId}", CurrentUserId, user.Id);

            return NoContent();
        }

        // GET: api/PortfolioUser/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPortfolioUser(int id)
        {
            var user = await _context.PortfolioUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(pu => pu.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user);
        }

        // POST: api/PortfolioUser
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreatePortfolioUser(PortfolioUser newUser)
        {
            _context.PortfolioUsers.Add(newUser);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetPortfolioUser), new { id = newUser.Id }, newUser);
        }

        // PUT: api/PortfolioUser/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePortfolioUser(int id, PortfolioUser updatedUser)
        {
            if (id != updatedUser.Id)
                return BadRequest("Route id does not match portfolio user id.");

            var user = await _context.PortfolioUsers.FindAsync(id);
            if (user == null)
                return NotFound();

            // Only the owner or an Admin may update this record.
            if (!IsAdmin && user.ApplicationUserId != CurrentUserId)
                return Forbid();

            user.UserName = updatedUser.UserName;
            user.FirstName = updatedUser.FirstName;
            user.LastName = updatedUser.LastName;
            user.Bio = updatedUser.Bio;
            user.ProfileImageUrl = updatedUser.ProfileImageUrl;
            // NOTE: intentionally do NOT copy ApplicationUserId — prevents re-linking.

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated portfolio user {PortfolioUserId}", id);

            return NoContent();
        }

        // DELETE: api/PortfolioUser/{id}
        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeletePortfolioUser(int id)
        {
            var user = await _context.PortfolioUsers.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            // Projects and Skills are removed automatically via cascade delete
            _context.PortfolioUsers.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted portfolio user {PortfolioUserId} and related projects/skills", id);

            return NoContent();
        }
    }
}
