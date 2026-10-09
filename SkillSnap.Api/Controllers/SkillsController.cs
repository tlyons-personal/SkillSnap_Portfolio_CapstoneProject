using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SkillSnap.Shared.Models;
using SkillSnap.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using SkillSnap.Shared.DTOs;

namespace SkillSnap.Api.Controllers
{
    // CRUD for portfolio skills, backed by per-user in-memory caching.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] 
    public class SkillsController : ApiControllerBase
    {
        private readonly SkillSnapContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SkillsController> _logger;

        public SkillsController(SkillSnapContext context, IMemoryCache cache, ILogger<SkillsController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        private static readonly Expression<Func<Skill, SkillDto>> ToDto = s => new SkillDto
        {
            Id = s.Id,
            Name = s.Name,
            Level = s.Level,
            PortfolioUserId = s.PortfolioUserId,
            PortfolioUserName = s.PortfolioUser != null ? s.PortfolioUser.UserName : null
        };

        // GET: api/Skills
        [HttpGet]
        public async Task<IActionResult> GetSkills()
        {
            // The JWT stores the Identity user id as NameIdentifier / sub.
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Cache per-user so one user's list is never served to another.
            var cacheKey = $"skills_{userId}";
            
            if (_cache.TryGetValue(cacheKey, out List<SkillDto>? skills))
            {
                _logger.LogInformation("Cache HIT for key '{CacheKey}'", cacheKey);
            }
            else
            {
                _logger.LogInformation("Cache MISS for key '{CacheKey}' — querying database", cacheKey);
                skills = await _context.Skills
                    .AsNoTracking()
                    .Where(s => s.PortfolioUser != null 
                             && s.PortfolioUser.ApplicationUserId == userId)
                    .Select(ToDto)
                    .ToListAsync();
                _cache.Set(cacheKey, skills, TimeSpan.FromMinutes(5));
                _logger.LogInformation("Cache populated for key '{CacheKey}' from DB", cacheKey);
            }
            return Ok(skills);
        }
        // GET: api/Skills/user/{portfolioUserId} — Managers/Admins view any user's skills.
        [HttpGet("user/{portfolioUserId:int}")]
        [Authorize(Policy = "CanReadAll")]
        public async Task<IActionResult> GetSkillsForUser(int portfolioUserId)
        {
            var skills = await _context.Skills
                .AsNoTracking()
                .Where(s => s.PortfolioUserId == portfolioUserId)
                .Select(ToDto)
                .ToListAsync();

            return Ok(skills);
        }
        // GET: api/Skills/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSkill(int id)
        {
            var cacheKey = $"skill_{id}";
            if (_cache.TryGetValue(cacheKey, out SkillDto? skill))
            {
                _logger.LogInformation("Cache HIT for key '{CacheKey}'", cacheKey);
            }
            else
            {
                _logger.LogInformation("Cache MISS for key '{CacheKey}' — querying database", cacheKey);
                skill = await _context.Skills
                    .AsNoTracking()
                    .Where(s => s.Id == id)
                    .Select(ToDto)
                    .FirstOrDefaultAsync();
                if (skill == null)
                {
                    return NotFound();
                }
                _cache.Set(cacheKey, skill, TimeSpan.FromMinutes(5));
                _logger.LogInformation("Cache populated for key '{CacheKey}' from DB", cacheKey);
            }
            return Ok(skill);
        }

        // POST: api/Skills
        [HttpPost]
        public async Task<IActionResult> CreateSkill(Skill skill)
        {
            var userId = CurrentUserId;

            var portfolio = await _context.PortfolioUsers
                .FirstOrDefaultAsync(pu => pu.ApplicationUserId == userId);
            if (portfolio == null)
                return BadRequest("Portfolio not found for the current user.");

            skill.PortfolioUserId = portfolio.Id;

            _context.Skills.Add(skill);
            await _context.SaveChangesAsync();

            // Invalidate the cache for skills after creating a new skill
            _cache.Remove($"skills_{userId}");

            _logger.LogInformation("Created skill {SkillId} and invalidated related cache entries", skill.Id);

            // Return a flat DTO to avoid serializing the Skill/PortfolioUser navigation cycle.
            var dto = new SkillDto
            {
                Id = skill.Id,
                Name = skill.Name,
                Level = skill.Level,
                PortfolioUserId = skill.PortfolioUserId,
                PortfolioUserName = portfolio.UserName
            };

            return CreatedAtAction(nameof(GetSkill), new { id = skill.Id }, dto);
        }

        // PUT: api/Skills/{id}
        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")] 
        public async Task<IActionResult> UpdateSkill(int id, Skill updatedSkill)
        {
            if (id != updatedSkill.Id)
            {
                return BadRequest("Route id does not match skill id.");
            }

            var skill = await _context.Skills.FindAsync(id);
            if (skill == null)
            {
                return NotFound();
            }

            var originalPortfolioUserId = skill.PortfolioUserId;

            // Update the editable fields
            skill.Name = updatedSkill.Name;
            skill.Level = updatedSkill.Level;
            skill.PortfolioUserId = updatedSkill.PortfolioUserId;

            await _context.SaveChangesAsync();

            // Clear every per-user bucket the change could affect, plus this item.
            var affectedOwners = await _context.PortfolioUsers
                .Where(pu => pu.Id == originalPortfolioUserId || pu.Id == skill.PortfolioUserId)
                .Select(pu => pu.ApplicationUserId)
                .ToListAsync();

            _cache.Remove($"skill_{id}");
            foreach (var ownerId in affectedOwners)
            {
                if (!string.IsNullOrEmpty(ownerId))
                    _cache.Remove($"skills_{ownerId}");
            }

            _logger.LogInformation("Updated skill {SkillId} and invalidated related cache entries", id);

            return NoContent();
        }

        // DELETE: api/Skills/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = Roles.Admin)] // optional: restrict deletes to admins
        public async Task<IActionResult> DeleteSkill(int id)
        {
            var skill = await _context.Skills
                .Include(s => s.PortfolioUser)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (skill == null) return NotFound();

            var isOwner = skill.PortfolioUser?.ApplicationUserId == CurrentUserId;
            if (!IsAdmin && !isOwner)
                return Forbid();

            _context.Skills.Remove(skill);
            await _context.SaveChangesAsync();

            _cache.Remove($"skills_{skill.PortfolioUser?.ApplicationUserId}");
            _cache.Remove($"skill_{id}");

            _logger.LogInformation("Deleted skill {SkillId} and invalidated related cache entries", id);

            return NoContent();
        }
    }
}