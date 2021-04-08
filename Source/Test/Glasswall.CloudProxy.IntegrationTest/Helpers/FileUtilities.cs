using System;
using System.IO;
using System.Threading.Tasks;

namespace Glasswall.CloudProxy.IntegrationTest.Helpers
{
    public class FileUtilities
    {
        public static async Task<string> GetBase64FromFileAsync()
        {
            return Convert.ToBase64String(await File.ReadAllBytesAsync(Constants.SAMPLE_PDF_FILE_PATH));
        }

        public static async Task<byte[]> GetBytesFromFileAsync()
        {
            return await File.ReadAllBytesAsync(Constants.SAMPLE_PDF_FILE_PATH);
        }
    }
}
