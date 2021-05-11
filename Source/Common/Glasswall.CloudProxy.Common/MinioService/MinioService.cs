using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel;
using Minio.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Glasswall.CloudProxy.Common.MinioService
{
    public class MinioService : IMinioService
    {
        private readonly MinioClient _minioClient;
        private readonly ILogger<MinioService> _logger;
        private readonly int expiresInt = 60 * 60 * 24;

        public MinioService(ILogger<MinioService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _minioClient = new MinioClient("minio-server.minio.svc.cluster.local:9000",
                                       "minio",
                                       "minio123"
                                 );
        }

        public async Task<(bool Status, string Error)> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
        {
            try
            {
                bool found = await _minioClient.BucketExistsAsync(bucketName, cancellationToken);
                return (found, null);
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, $"Error Processing '{nameof(BucketExistsAsync)}' and error detail is {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Status, string Error)> MakeBucketAsync(string bucketName, CancellationToken cancellationToken = default)
        {
            try
            {
                (bool Status, string Error) = await BucketExistsAsync(bucketName, cancellationToken);
                if (!Status)
                {
                    await _minioClient.MakeBucketAsync(bucketName, cancellationToken: cancellationToken);
                }
                return (true, null);
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, $"Error Processing '{nameof(MakeBucketAsync)}' and error detail is {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<(string URL, string Error)> PresignedGetObjectAsync(string bucketName, string objectName)
        {
            try
            {
                string url = await _minioClient.PresignedGetObjectAsync(bucketName, objectName, expiresInt);
                return (url, null);
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, $"Error Processing '{nameof(PresignedGetObjectAsync)}' and error detail is {ex.Message}");
                return (null, ex.Message);
            }
        }

        public async Task<(bool Status, string Error)> PutObjectAsync(string bucketName, string objectName, Stream fileStream, Dictionary<string, string> metaData = null, CancellationToken cancellationToken = default)
        {
            try
            {
                Aes aesEncryption = Aes.Create();
                aesEncryption.KeySize = 256;
                aesEncryption.GenerateKey();
                SSEC ssec = new SSEC(aesEncryption.Key);
                await _minioClient.PutObjectAsync(bucketName,
                                           objectName,
                                            fileStream,
                                            fileStream.Length,
                                          Constants.OCTET_STREAM_CONTENT_TYPE, metaData, null, cancellationToken);
                return (true, null);
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, $"Error Processing '{nameof(PutObjectAsync)}' and error detail is {ex.Message}");
                return (false, ex.Message);
            }
        }
    }
}
