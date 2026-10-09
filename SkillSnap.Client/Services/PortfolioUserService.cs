using System.Net.Http.Json;
using System.Text.Json;
using SkillSnap.Shared.Models;
using SkillSnap.Shared.DTOs;

namespace SkillSnap.Client.Services
{
    // Client-side calls to the portfolio user profile endpoints.
    public class PortfolioUserService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _json;

        public PortfolioUserService(HttpClient httpClient, JsonSerializerOptions json)
        {
            _httpClient = httpClient;
            _json = json;
        }

        public async Task<PortfolioUser?> GetMyProfileAsync(CancellationToken ct = default)
        {
            return await _httpClient.GetFromJsonAsync<PortfolioUser?>("api/portfoliouser/me", _json, ct);
        }

        public async Task<List<PortfolioUser>> GetAllAsync(CancellationToken ct = default)
        {
            var users = await _httpClient.GetFromJsonAsync<List<PortfolioUser>>("api/portfoliouser", _json, ct);
            return users ?? new List<PortfolioUser>();
        }

        public async Task<PortfolioUser?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _httpClient.GetFromJsonAsync<PortfolioUser?>($"api/portfoliouser/{id}", _json, ct);
        }

        public async Task UpdateMyProfileAsync(PortfolioUser user, CancellationToken ct = default)
        {
            var response = await _httpClient.PutAsJsonAsync("api/portfoliouser/me", user, _json, ct);
            response.EnsureSuccessStatusCode();
        }
    }
}
