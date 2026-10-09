using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkillSnap.Api.Models;
using SkillSnap.Shared.Models;

namespace SkillSnap.Api.Controllers
{
    // Seeds the database with a sample admin user plus demo projects and skills.
    [ApiController]
    [Route("api/[controller]")]
    public class SeedController : ControllerBase
    {
        private readonly SkillSnapContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public SeedController(
            SkillSnapContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // POST: api/Seed — only runs once, when no portfolio data exists yet.
        [HttpPost]
        public async Task<IActionResult> Seed()
        {
            if (_context.PortfolioUsers.Any())
            {
                return BadRequest("Sample data already exists.");
            }

            // Ensure the Admin role exists before assigning it.
            if (!await _roleManager.RoleExistsAsync(Roles.Admin))
                await _roleManager.CreateAsync(new IdentityRole(Roles.Admin));

            // Create the Identity login for Jordan.
            var jordan = new ApplicationUser
            {
                UserName = "jordan@skillsnap.dev"
            };
            var createResult = await _userManager.CreateAsync(jordan, "AdminTad#12345");
            if (!createResult.Succeeded)
                return BadRequest(createResult.Errors);

            await _userManager.AddToRoleAsync(jordan, Roles.Admin);

            var user = new PortfolioUser
            {
                UserName = "Jordan Developer",
                FirstName = "Jordan",
                LastName = "Williams",
                Bio = "Full-stack developer passionate about learning new tech.",
                ProfileImageUrl = "https://example.com/images/jordan.png",
                ApplicationUserId = jordan.Id,
                Projects = new List<Project>
                {
                    new Project { Title = "Task Tracker", Description = "Manage tasks effectively", ImageUrl = "https://example.com/images/task.png" },
                    new Project { Title = "Weather App", Description = "Forecast weather using APIs", ImageUrl = "https://example.com/images/weather.png" }
                },
                Skills = new List<Skill>
                {
                    new Skill { Name = "C#", Level = SkillLevel.Advanced },
                    new Skill { Name = "Blazor", Level = SkillLevel.Intermediate }
                }
            };
            _context.PortfolioUsers.Add(user);
            await _context.SaveChangesAsync();
            return Ok("Sample data inserted.");
        }
    }
}
