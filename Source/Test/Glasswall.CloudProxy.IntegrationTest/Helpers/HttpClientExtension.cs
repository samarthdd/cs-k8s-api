using Glasswall.CloudProxy.Common.Web.Models;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Glasswall.CloudProxy.IntegrationTest.Helpers
{
    public static class HttpClientExtension
    {
        public static async Task<HttpResponseMessage> SendBase64Async(this HttpClient client, string uri)
        {
            Base64Request base64Request = new Base64Request
            {
                Base64 = await FileUtilities.GetBase64FromFileAsync()
            };
            string json = JsonConvert.SerializeObject(base64Request);
            StringContent stringContent = new StringContent(json, Encoding.UTF8, Constants.JSON_MEDIA_TYPE);
            HttpResponseMessage response = await client.PostAsync(uri, stringContent);
            return response;
        }

        public static async Task<HttpResponseMessage> SendBase64RequiredAsync(this HttpClient client, string uri)
        {
            Base64Request base64Request = new Base64Request();
            string json = JsonConvert.SerializeObject(base64Request);
            StringContent stringContent = new StringContent(json, Encoding.UTF8, Constants.JSON_MEDIA_TYPE);
            HttpResponseMessage response = await client.PostAsync(uri, stringContent);
            return response;
        }

        public static async Task<HttpResponseMessage> SendFileRequiredAsync(this HttpClient client, string uri)
        {
            MultipartFormDataContent form = new MultipartFormDataContent
            {
            };
            HttpResponseMessage response = await client.PostAsync(uri, form);
            return response;
        }

        public static async Task<HttpResponseMessage> SendFileAsync(this HttpClient client, string uri)
        {
            byte[] fileBytes = await FileUtilities.GetBytesFromFileAsync();
            MultipartFormDataContent form = new MultipartFormDataContent
            {
                { new ByteArrayContent(fileBytes, 0, fileBytes.Length), Constants.FILE, Constants.FILE_NAME }
            };
            HttpResponseMessage response = await client.PostAsync(uri, form);
            return response;
        }
    }
}
