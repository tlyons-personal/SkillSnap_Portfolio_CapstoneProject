using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Shared.DTOs
{
    public class PromoteRequest
    {
        [Required]
        public string? UserName { get; set; }

        [Required]
        public string? Role { get; set; }
    }
}
