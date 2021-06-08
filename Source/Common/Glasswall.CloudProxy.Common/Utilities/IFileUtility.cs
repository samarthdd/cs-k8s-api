using Microsoft.AspNetCore.Http;
using System;

namespace Glasswall.CloudProxy.Common.Utilities
{
    public interface IFileUtility : IDisposable
    {
        bool TryReadFormFile(IFormFile formFile, out byte[] file);

        bool TryGetBase64File(string base64File, out byte[] file);
    }
}
