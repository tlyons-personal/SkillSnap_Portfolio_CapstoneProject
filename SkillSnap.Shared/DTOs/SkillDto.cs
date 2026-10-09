using SkillSnap.Shared.Models;

namespace SkillSnap.Shared.DTOs
{
    public class SkillDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public SkillLevel? Level { get; set; }
        
        public int PortfolioUserId { get; set; }
        public string? PortfolioUserName { get; set; }
        }
}