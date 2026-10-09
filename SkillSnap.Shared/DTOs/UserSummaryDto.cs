namespace SkillSnap.Shared.DTOs
{
    public class UserSummaryDto
    {
        public string UserName { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
