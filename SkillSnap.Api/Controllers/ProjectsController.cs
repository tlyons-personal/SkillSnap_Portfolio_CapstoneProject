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
    // CRUD for portfolio projects, backed by per-user in-memory caching.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] 
    public class ProjectsController : ApiControllerBase
    {
        private readonly SkillSnapContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(SkillSnapContext context, IMemoryCache cache, ILogger<ProjectsController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        private static readonly Expression<Func<Project, ProjectDto>> ToDto = p => new ProjectDto
        {
            Id = p.Id,
            Title = p.Title,
            Description = p.Description,
            ImageUrl = p.ImageUrl,
            PortfolioUserId = p.PortfolioUserId,
            PortfolioUserName = p.PortfolioUser != null ? p.PortfolioUser.UserName : null,
            PortfolioUserFirstName = p.PortfolioUser != null ? p.PortfolioUser.FirstName : null,
            PortfolioUserLastName = p.PortfolioUser != null ? p.PortfolioUser.LastName : null
        };

        // GET: api/Projects
        [HttpGet]
        public async Task<IActionResult> GetProjects()
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            
            // Different cache bucket for "all" vs. a specific user's list.
            var cacheKey = CanReadAll ? "projects_all" : $"projects_{userId}";

            if (_cache.TryGetValue(cacheKey, out List<ProjectDto>? projects))
            {
                _logger.LogInformation("Cache HIT for key '{CacheKey}'", cacheKey);
            }
            else
            {
                _logger.LogInformation("Cache MISS for key '{CacheKey}' — querying database", cacheKey);
                var query = _context.Projects.AsNoTracking().AsQueryable();

                // Regular users are scoped to their own portfolio.
                if (!CanReadAll)
                    query = query.Where(p => p.PortfolioUser != null
                                          && p.PortfolioUser.ApplicationUserId == userId);
                projects = await query
                    .Select(ToDto)
                    .ToListAsync();

                _cache.Set(cacheKey, projects, TimeSpan.FromMinutes(5));
                _logger.LogInformation("Cache populated for key '{CacheKey}' from DB", cacheKey);
            }
            return Ok(projects);
        }

        // GET: api/Projects/user/{portfolioUserId} — Managers/Admins view any user's projects.
        [HttpGet("user/{portfolioUserId:int}")]
        [Authorize(Policy = "CanReadAll")]
        public async Task<IActionResult> GetProjectsForUser(int portfolioUserId)
        {
            var projects = await _context.Projects
                .AsNoTracking()
                .Where(p => p.PortfolioUserId == portfolioUserId)
                .Select(ToDto)
                .ToListAsync();

            return Ok(projects);
        }

        // GET: api/Projects/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProject(int id)
        {
            var cacheKey = $"project_{id}";
            if (_cache.TryGetValue(cacheKey, out ProjectDto? project))
            {
                _logger.LogInformation("Cache HIT for key '{CacheKey}'", cacheKey);
            }
            else
            {
                _logger.LogInformation("Cache MISS for key '{CacheKey}' — querying database", cacheKey);
                project = await _context.Projects
                    .AsNoTracking()
                    .Where(p => p.Id == id)
                    .Select(ToDto)
                    .FirstOrDefaultAsync();
                if (project == null)
                {
                    return NotFound();
                }
                _cache.Set(cacheKey, project, TimeSpan.FromMinutes(5));
                _logger.LogInformation("Cache populated for key '{CacheKey}' from DB", cacheKey);
            }
            return Ok(project);
        }

        // POST: api/Projects
        [HttpPost]
        public async Task<IActionResult> CreateProject(Project project)
        {
            PortfolioUser? portfolio;

            if (IsAdmin && project.PortfolioUserId > 0)
            {
                // Admin may create a project for any existing portfolio.
                portfolio = await _context.PortfolioUsers
                    .FirstOrDefaultAsync(pu => pu.Id == project.PortfolioUserId);
            }
            else
            {
                // Everyone else creates for their own portfolio only.
                portfolio = await _context.PortfolioUsers
                    .FirstOrDefaultAsync(pu => pu.ApplicationUserId == CurrentUserId);
            }

            if (portfolio == null)
                return BadRequest("Portfolio not found.");

            project.PortfolioUserId = portfolio.Id;
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            _cache.Remove($"projects_{portfolio.ApplicationUserId}");
            _cache.Remove("projects_all");

            _logger.LogInformation("Created project {ProjectId} and invalidated related cache entries", project.Id);

            // Return a flat DTO to avoid serializing the Project/PortfolioUser navigation cycle.
            var dto = new ProjectDto
            {
                Id = project.Id,
                Title = project.Title,
                Description = project.Description,
                ImageUrl = project.ImageUrl,
                PortfolioUserId = project.PortfolioUserId,
                PortfolioUserName = portfolio.UserName,
                PortfolioUserFirstName = portfolio.FirstName,
                PortfolioUserLastName = portfolio.LastName
            };

            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, dto);
        }

        // PUT: api/Projects/{id}
        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")] 
        public async Task<IActionResult> UpdateProject(int id, Project updatedProject)
        {
            if (id != updatedProject.Id)
            {
                return BadRequest("Route id does not match project id.");
            }

            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            var originalPortfolioUserId = project.PortfolioUserId;

            // Update the editable fields
            project.Title = updatedProject.Title;
            project.Description = updatedProject.Description;
            project.ImageUrl = updatedProject.ImageUrl;
            project.PortfolioUserId = updatedProject.PortfolioUserId;

            await _context.SaveChangesAsync();

            // Clear every per-user bucket the change could affect, plus the shared list and this item.
            var affectedOwners = await _context.PortfolioUsers
                .Where(pu => pu.Id == originalPortfolioUserId || pu.Id == project.PortfolioUserId)
                .Select(pu => pu.ApplicationUserId)
                .ToListAsync();

            _cache.Remove("projects_all");
            _cache.Remove($"project_{id}");
            foreach (var ownerId in affectedOwners)
            {
                if (!string.IsNullOrEmpty(ownerId))
                    _cache.Remove($"projects_{ownerId}");
            }

            _logger.LogInformation("Updated project {ProjectId} and invalidated related cache entries", id);

            return NoContent();
        }

        // DELETE: api/Projects/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = Roles.Admin)] // optional: restrict deletes to admins
        public async Task<IActionResult> DeleteProject(int id)
        {
            var project = await _context.Projects
                .Include(p => p.PortfolioUser)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            // Allow if Admin, or the caller owns the parent portfolio.
            var isOwner = project.PortfolioUser?.ApplicationUserId == CurrentUserId;
            if (!IsAdmin && !isOwner)
                return Forbid();

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            _cache.Remove($"projects_{project.PortfolioUser?.ApplicationUserId}");
            _cache.Remove("projects_all");
            _cache.Remove($"project_{id}");

            _logger.LogInformation("Deleted project {ProjectId} and invalidated related cache entries", id);

            return NoContent();
        }
    }
}