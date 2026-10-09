namespace SkillSnap.Shared.Models
{
    public static class PortfolioUserExtensions
    {
        // Returns the trimmed "First Last" display name, or the supplied fallback when no name is set.
        public static string FullNameOr(this PortfolioUser user, string fallback)
        {
            var name = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }
    }
}
