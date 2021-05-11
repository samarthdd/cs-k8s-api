using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Glasswall.CloudProxy.Common.MinioService
{
    public interface IMinioService
    {
        Task<(bool Status, string Error)> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default);
        Task<(bool Status, string Error)> MakeBucketAsync(string bucketName, CancellationToken cancellationToken = default);
        Task<(bool Status, string Error)> PutObjectAsync(string bucketName, string objectName, Stream fileStream, Dictionary<string, string> metaData = null, CancellationToken cancellationToken = default);
        Task<(string URL, string Error)> PresignedGetObjectAsync(string bucketName, string objectName);
    }
}
