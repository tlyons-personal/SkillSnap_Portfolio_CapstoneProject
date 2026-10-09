using System.Net.Http.Json;
using System.Text.Json;
using SkillSnap.Shared.DTOs;

namespace SkillSnap.Client.Services
{
    public record PromoteResult(bool Success, string? Message = null);

    // Client-side calls to the admin endpoints: list users, promote, and delete.
    public class AdminService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _json;

        public AdminService(HttpClient httpClient, JsonSerializerOptions json)
        {
            _httpClient = httpClient;
            _json = json;
        }

        public async Task<List<UserSummaryDto>> GetUsersAsync(CancellationToken ct = default)
        {
            var users = await _httpClient.GetFromJsonAsync<List<UserSummaryDto>>("api/auth/users", _json, ct);
            return users ?? new List<UserSummaryDto>();
        }

        public async Task<PromoteResult> PromoteAsync(string userName, string role, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/auth/promote", new PromoteRequest { UserName = userName, Role = role }, _json, ct);

            var payload = await TryReadMessageAsync(response, ct);

            return response.IsSuccessStatusCode
                ? new PromoteResult(true, payload ?? "User promoted.")
                : new PromoteResult(false, payload ?? "Promotion failed. Please try again.");
        }

        public async Task<PromoteResult> DeleteUserAsync(string userName, CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync(
                $"api/auth/users/{Uri.EscapeDataString(userName)}", ct);

            if (response.IsSuccessStatusCode)
                return new PromoteResult(true, $"{userName} deleted.");

            var payload = await TryReadMessageAsync(response, ct);
            return new PromoteResult(false, payload ?? "Delete failed. Please try again.");
        }

        private static async Task<string?> TryReadMessageAsync(HttpResponseMessage response, CancellationToken ct)
        {
            try
            {
                var result = await response.Content.ReadFromJsonAsync<MessageResponse>(cancellationToken: ct);
                return result?.Message;
            }
            catch
            {
                return null;
            }
        }

        private record MessageResponse(string? Message);
    }
}
