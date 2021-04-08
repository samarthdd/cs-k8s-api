using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Glasswall.CloudProxy.Api.Utilities
{
    public class FileUtility : IFileUtility
    {
        private readonly ILogger<FileUtility> _logger;

        public FileUtility(ILogger<FileUtility> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool TryReadFormFile(IFormFile formFile, out byte[] file)
        {
            file = null;

            try
            {
                using MemoryStream ms = new MemoryStream();
                formFile.CopyTo(ms);
                file = ms.ToArray();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Could not parse input file.");
            }

            return file?.Length > 0;
        }

        public bool TryGetBase64File(string base64File, out byte[] file)
        {
            file = null;

            try
            {
                file = Convert.FromBase64String(base64File);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not parse base64 file {0}", base64File);
            }

            int fileSize = file?.Length ?? 0;
            return fileSize > 0;
        }
    }
}
