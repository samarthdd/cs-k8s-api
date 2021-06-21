using Glasswall.CloudProxy.Api;
using Glasswall.CloudProxy.IntegrationTest.Helpers;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Glasswall.CloudProxy.IntegrationTest.ControllerTests
{
    public class AnalyseControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;
        private readonly HttpClient _httpClient;

        public AnalyseControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _httpClient = _factory.CreateClient();
        }

        [Theory]
        [InlineData(Constants.Endpoints.ANALYSE_BASE64)]
        public async Task AnalyseFromBase64_Required_Check(string url)
        {
            HttpResponseMessage response = await _httpClient.SendBase64RequiredAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(Constants.Endpoints.ANALYSE_BASE64)]
        public async Task AnalyseFromBase64(string url)
        {
            HttpResponseMessage response = await _httpClient.SendBase64Async(url);
            response.EnsureSuccessStatusCode();
            Assert.Equal(Constants.OCTET_STREAM_MEDIA_TYPE, response.Content.Headers.ContentType.ToString());
            Assert.True(response.Content.Headers.Contains(Constants.FILE_ID_HEADER));
        }
    }
}
