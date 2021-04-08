using Glasswall.CloudProxy.Api;
using Glasswall.CloudProxy.IntegrationTest.Helpers;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Glasswall.CloudProxy.IntegrationTest.ControllerTests
{
    public class RebuildControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        readonly CustomWebApplicationFactory<Startup> _factory;
        readonly HttpClient _httpClient;

        public RebuildControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _httpClient = _factory.CreateClient();
        }

        [Theory]
        [InlineData(Constants.Endpoints.REBUILD_BASE64)]
        public async Task RebuildFromBase64_Required_Check(string url)
        {
            HttpResponseMessage response = await _httpClient.SendBase64RequiredAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(Constants.Endpoints.REBUILD_BASE64)]
        public async Task RebuildFromBase64(string url)
        {
            HttpResponseMessage response = await _httpClient.SendBase64Async(url);
            response.EnsureSuccessStatusCode();
            Assert.Equal(Constants.OCTET_STREAM_MEDIA_TYPE, response.Content.Headers.ContentType.ToString());
            Assert.True(response.Content.Headers.Contains(Constants.FILE_ID_HEADER));
        }

        [Theory]
        [InlineData(Constants.Endpoints.REBUILD_FILE)]
        public async Task RebuildFromFormFile_Required_Check(string url)
        {
            HttpResponseMessage response = await _httpClient.SendFileRequiredAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(Constants.Endpoints.REBUILD_FILE)]
        public async Task RebuildFromFormFile(string url)
        {
            HttpResponseMessage response = await _httpClient.SendFileAsync(url);
            response.EnsureSuccessStatusCode();
            Assert.Equal(Constants.OCTET_STREAM_MEDIA_TYPE, response.Content.Headers.ContentType.ToString());
            Assert.True(response.Content.Headers.Contains(Constants.FILE_ID_HEADER));
        }
    }
}
