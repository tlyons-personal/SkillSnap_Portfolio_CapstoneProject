using System.Net.Http.Headers;
using Blazored.LocalStorage;

namespace SkillSnap.Client.Auth
{
    // Attaches the stored JWT as a Bearer header on every outgoing API request.
    public class AuthTokenHandler : DelegatingHandler
    {
        private readonly ILocalStorageService _localStorage;

        public AuthTokenHandler(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = await _localStorage.GetItemAsStringAsync(TokenKeyRef, cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return await base.SendAsync(request, cancellationToken);
        }

        private const string TokenKeyRef = JwtAuthenticationStateProvider.TokenKey;
    }
}