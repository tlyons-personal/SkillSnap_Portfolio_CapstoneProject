using System.Text.Json;
using SkillSnap.Shared.DTOs;
using SkillSnap.Shared.Models;

namespace SkillSnap.Client.Services
{
    // Project CRUD over api/projects, caching the list in the user session.
    public class ProjectService : CachedCrudService<ProjectDto, Project>
    {
        private readonly UserSessionService _session;

        public ProjectService(HttpClient httpClient, JsonSerializerOptions json, UserSessionService session)
            : base(httpClient, json)
        {
            _session = session;
        }

        protected override string Endpoint => "api/projects";

        protected override List<ProjectDto>? Cache
        {
            get => _session.Projects;
            set => _session.Projects = value;
        }

        protected override int IdOf(ProjectDto dto) => dto.Id;
    }
}