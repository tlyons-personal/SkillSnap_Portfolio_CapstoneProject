using SkillSnap.Shared.DTOs;

namespace SkillSnap.Client.Services
{
    // Session-scoped state so components share loaded portfolio data instead of each re-fetching it.
    public class UserSessionService
    {
        public List<ProjectDto>? Projects { get; set; }
        public List<SkillDto>? Skills { get; set; }

        public void Clear()
        {
            Projects = null;
            Skills = null;
        }
    }
}