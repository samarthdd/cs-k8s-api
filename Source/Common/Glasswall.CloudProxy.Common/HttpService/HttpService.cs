using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Glasswall.CloudProxy.Common.HttpService
{
    public class HttpService : IHttpService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpService> _logger;

        public HttpService(ILogger<HttpService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient();
        }

        public async Task<(byte[] Data, string Error)> GetFileBytes(string uri)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadAsByteArrayAsync(), null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Processing '{nameof(GetFileBytes)}' and error detail is {ex.Message}");
                return (null, ex.Message);
            }
        }
    }
}
