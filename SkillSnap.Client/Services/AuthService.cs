using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using SkillSnap.Client.Auth;

namespace SkillSnap.Client.Services
{
    public record LoginRequest(string UserName, string Password);
    public record RegisterRequest(string UserName, string Password, string ConfirmPassword);
    public record AuthResult(bool Success, string? Error = null);

    // Handles login, registration, logout, and account deletion; stores the JWT.
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly JwtAuthenticationStateProvider _authStateProvider;
        private readonly UserSessionService _session;
        private readonly JsonSerializerOptions _json;

        public AuthService(
            HttpClient httpClient,
            ILocalStorageService localStorage,
            JwtAuthenticationStateProvider authStateProvider,
            UserSessionService session,
            JsonSerializerOptions json)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _authStateProvider = authStateProvider;
            _session = session;
            _json = json;
        }

        public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, _json, ct);

            if (!response.IsSuccessStatusCode)
                return new AuthResult(false, await ReadErrorAsync(response, ct));

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(_json, ct);
            if (string.IsNullOrWhiteSpace(payload?.Token))
                return new AuthResult(false, "No token returned by the server.");

            await _localStorage.SetItemAsStringAsync(
                JwtAuthenticationStateProvider.TokenKey, payload.Token, ct);
            _authStateProvider.NotifyUserAuthentication(payload.Token);
            _session.Clear(); // drop any data cached for a previous user

            return new AuthResult(true);
        }

        public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, _json, ct);

            return response.IsSuccessStatusCode
                ? new AuthResult(true)
                : new AuthResult(false, await ReadErrorAsync(response, ct));
        }

        public async Task LogoutAsync()
        {
            await _localStorage.RemoveItemAsync(JwtAuthenticationStateProvider.TokenKey);
            _authStateProvider.NotifyUserLogout();
            _session.Clear();
        }

        public async Task<AuthResult> DeleteMyAccountAsync(CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync("api/auth/me", ct);

            if (!response.IsSuccessStatusCode)
                return new AuthResult(false, await ReadErrorAsync(response, ct));

            // Account is gone — clear the stored token and sign out.
            await LogoutAsync();
            return new AuthResult(true);
        }

        private async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
        {
            try
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(_json, ct);
                if (!string.IsNullOrWhiteSpace(error?.Message))
                    return error!.Message!;
            }
            catch
            {
                // Server may return ModelState/ProblemDetails instead — fall through.
            }
            return "Request failed. Please check your details and try again.";
        }

        private record TokenResponse(string? Token);
        private record ErrorResponse(string? Message);
    }
}