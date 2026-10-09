namespace SkillSnap.Shared.DTOs
{
    public class ProjectDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        
        public int PortfolioUserId { get; set; }
        public string? PortfolioUserName { get; set; }
        public string? PortfolioUserFirstName { get; set; }
        public string? PortfolioUserLastName { get; set; }

        // Trimmed "First Last", falling back to the user name when no real name is set.
        public string PortfolioUserDisplayName
        {
            get
            {
                var name = $"{PortfolioUserFirstName} {PortfolioUserLastName}".Trim();
                return string.IsNullOrWhiteSpace(name) ? (PortfolioUserName ?? string.Empty) : name;
            }
        }
    }
}