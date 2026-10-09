using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkillSnap.Shared.Models
{

    public class PortfolioUser
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public required string UserName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }

        // IdentityUser.Id is a string (GUID), so the FK is a string.
        public string? ApplicationUserId { get; set; }

        public List<Project>? Projects { get; set; }
        public List<Skill>? Skills { get; set; }
    }
}