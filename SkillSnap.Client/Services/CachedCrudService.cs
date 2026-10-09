using System.Net.Http.Json;
using System.Text.Json;

namespace SkillSnap.Client.Services
{
    // Shared CRUD + session-cache behaviour for the Project and Skill services.
    // Mutations update the cached list in place so callers avoid an extra GET round-trip.
    public abstract class CachedCrudService<TDto, TModel>
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _json;

        protected CachedCrudService(HttpClient http, JsonSerializerOptions json)
        {
            _http = http;
            _json = json;
        }

        protected abstract string Endpoint { get; }

        // Backed by the session cache slot in the derived service.
        protected abstract List<TDto>? Cache { get; set; }

        protected abstract int IdOf(TDto dto);

        public async Task<List<TDto>> GetAsync(bool forceRefresh = false, CancellationToken ct = default)
        {
            if (!forceRefresh && Cache is not null)
                return Cache;

            var items = await _http.GetFromJsonAsync<List<TDto>>(Endpoint, _json, ct);
            Cache = items ?? new List<TDto>();
            return Cache;
        }

        public async Task<List<TDto>> GetForUserAsync(int portfolioUserId, CancellationToken ct = default)
        {
            var items = await _http.GetFromJsonAsync<List<TDto>>(
                $"{Endpoint}/user/{portfolioUserId}", _json, ct);
            return items ?? new List<TDto>();
        }

        public async Task<TDto?> AddAsync(TModel model, CancellationToken ct = default)
        {
            var response = await _http.PostAsJsonAsync(Endpoint, model, _json, ct);
            response.EnsureSuccessStatusCode();

            var created = await response.Content.ReadFromJsonAsync<TDto>(_json, ct);
            if (created is not null)
                Cache?.Add(created);
            return created;
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var response = await _http.DeleteAsync($"{Endpoint}/{id}", ct);
            response.EnsureSuccessStatusCode();
            Cache?.RemoveAll(x => IdOf(x) == id);
        }
    }
}
