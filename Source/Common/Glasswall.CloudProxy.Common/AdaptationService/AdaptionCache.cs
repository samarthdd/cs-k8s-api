using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public class AdaptionDescriptor
    {
        private Guid uuid; // field
        private ReturnOutcome outcome;
        private string originalStoreFilePath;
        private string rebuiltStoreFilePath;
        private DateTime accessTime;
        public AdaptionDescriptor()
        {
            uuid = Guid.NewGuid();
            outcome = ReturnOutcome.GW_UNPROCESSED;
            accessTime = DateTime.Now;
        }

        public void Update(ReturnOutcome Outcome,
            string OriginalStoreFilePath,
            string RebuiltStoreFilePath)
        {
            accessTime = DateTime.Now;
            outcome = Outcome;
            originalStoreFilePath = OriginalStoreFilePath;
            rebuiltStoreFilePath = RebuiltStoreFilePath;
        }

        public Guid UUID => uuid;
        public ReturnOutcome Outcome => outcome;
        public string OriginalStoreFilePath => originalStoreFilePath;
        public string RebuiltStoreFilePath => rebuiltStoreFilePath;
        public DateTime AccessTime { get => accessTime; set => accessTime = value; }

    }

    public sealed class AdaptionCache
    {
        private static ConcurrentDictionary<string, AdaptionDescriptor> CacheMap;

        private AdaptionCache()
        {
            //int concurrencyLevel = 100;
            //int initialCapacity = 100;
            //CacheMap = new ConcurrentDictionary<String, AdaptionDescriptor>(concurrencyLevel, initialCapacity);
            CacheMap = new ConcurrentDictionary<string, AdaptionDescriptor>();
        }

        public static AdaptionCache Instance => Nested.instance;

        private static string BytesToString(byte[] bytes)
        {
            string result = "";
            foreach (byte b in bytes)
            {
                result += b.ToString("x2");
            }

            return result;
        }

        private static string GetFileHash(byte[] file)
        {
            SHA256 Sha256 = SHA256.Create();
            return BytesToString(Sha256.ComputeHash(file));
        }

        public AdaptionDescriptor GetDescriptor(byte[] file)
        {
            string fileHash = GetFileHash(file);

            if (!CacheMap.TryGetValue(fileHash, out AdaptionDescriptor descriptor))
            {
                descriptor = new AdaptionDescriptor();
                if (!CacheMap.TryAdd(fileHash, descriptor))
                {
                    //descriptor = null;
                }
            }

            return descriptor;
        }

        private class Nested
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested()
            {
            }

            internal static readonly AdaptionCache instance = new AdaptionCache();
        }
    }
}
