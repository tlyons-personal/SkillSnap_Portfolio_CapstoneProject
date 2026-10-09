using System.Text.Json;
using SkillSnap.Shared.DTOs;
using SkillSnap.Shared.Models;

namespace SkillSnap.Client.Services
{
    // Skill CRUD over api/skills, caching the list in the user session.
    public class SkillService : CachedCrudService<SkillDto, Skill>
    {
        private readonly UserSessionService _session;

        public SkillService(HttpClient httpClient, JsonSerializerOptions json, UserSessionService session)
            : base(httpClient, json)
        {
            _session = session;
        }

        protected override string Endpoint => "api/skills";

        protected override List<SkillDto>? Cache
        {
            get => _session.Skills;
            set => _session.Skills = value;
        }

        protected override int IdOf(SkillDto dto) => dto.Id;
    }
}