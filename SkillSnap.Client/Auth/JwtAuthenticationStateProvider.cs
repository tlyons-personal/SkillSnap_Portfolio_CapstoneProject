using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace SkillSnap.Client.Auth
{
    // Supplies Blazor's auth state by reading and parsing the stored JWT.
    public class JwtAuthenticationStateProvider : AuthenticationStateProvider
    {
        public const string TokenKey = "authToken";

        private readonly ILocalStorageService _localStorage;
        private readonly AuthenticationState _anonymous =
            new(new ClaimsPrincipal(new ClaimsIdentity()));
        
        public JwtAuthenticationStateProvider(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _localStorage.GetItemAsStringAsync(TokenKey);

            if (string.IsNullOrWhiteSpace(token))
                return _anonymous;

            // Parse the token once and reuse the claims for both the expiry check and the identity.
            var claims = ParseClaims(token);
            if (IsExpired(claims))
            {
                await _localStorage.RemoveItemAsync(TokenKey);
                return _anonymous;
            }

            var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        // Called by AuthService after a successful login.
        public void NotifyUserAuthentication(string token)
        {
            var identity = new ClaimsIdentity(ParseClaims(token), authenticationType: "jwt");
            var user = new ClaimsPrincipal(identity);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        // Called by AuthService on logout.
        public void NotifyUserLogout()
        {
            NotifyAuthenticationStateChanged(Task.FromResult(_anonymous));
        }

        private static bool IsExpired(IReadOnlyCollection<Claim> claims)
        {
            var exp = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            if (exp is null || !long.TryParse(exp, out var seconds))
                return true;

            var expiry = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return expiry <= DateTimeOffset.UtcNow;
        }

        private static List<Claim> ParseClaims(string token)
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return new List<Claim>();

            var json = DecodeBase64Url(parts[1]);
            var pairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                        ?? new Dictionary<string, JsonElement>();

            var claims = new List<Claim>();
            foreach (var (key, value) in pairs)
            {
                // A "role" claim can be a single value or an array.
                if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in value.EnumerateArray())
                        claims.Add(new Claim(MapClaimType(key), item.ToString()));
                }
                else
                {
                    claims.Add(new Claim(MapClaimType(key), value.ToString()));
                }
            }
            return claims;
        }

        // Map the short JWT names to the ClaimTypes that [Authorize] / User.Identity expect.
        private static string MapClaimType(string key) => key switch
        {
            "role" or "roles" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
                => ClaimTypes.Role,
            "unique_name" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
                => ClaimTypes.Name,
            "nameid" or "sub" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
                => ClaimTypes.NameIdentifier,
            _ => key
        };

        private static string DecodeBase64Url(string value)
        {
            value = value.Replace('-', '+').Replace('_', '/');
            switch (value.Length % 4)
            {
                case 2: value += "=="; break;
                case 3: value += "="; break;
            }
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
    }
}
