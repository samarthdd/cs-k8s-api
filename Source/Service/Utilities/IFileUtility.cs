﻿using Microsoft.AspNetCore.Http;

namespace Glasswall.CloudProxy.Api.Utilities
{
    public interface IFileUtility
    {
        bool TryReadFormFile(IFormFile formFile, out byte[] file);

        bool TryGetBase64File(string base64File, out byte[] file);
    }
}
