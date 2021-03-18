using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public class AdaptionDescriptor
    {
        private Guid uuid; // field
        ReturnOutcome outcome;
        string originalStoreFilePath;
        string rebuiltStoreFilePath;
        DateTime accessTime;
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

        public Guid UUID  { get { return uuid; } }
        public ReturnOutcome Outcome { get { return outcome; } }
        public string OriginalStoreFilePath { get { return originalStoreFilePath; } }
        public string RebuiltStoreFilePath { get { return rebuiltStoreFilePath; } }
        public DateTime AccessTime { get { return accessTime; } set { accessTime = value; } }

    }

    public sealed class AdaptionCache
    {
        static ConcurrentDictionary<String, AdaptionDescriptor> CacheMap;

        private AdaptionCache()
        {
            //int concurrencyLevel = 100;
            //int initialCapacity = 100;
            //CacheMap = new ConcurrentDictionary<String, AdaptionDescriptor>(concurrencyLevel, initialCapacity);
            CacheMap = new ConcurrentDictionary<String, AdaptionDescriptor>();
        }

        public static AdaptionCache Instance { get { return Nested.instance; } }

        private static string BytesToString(byte[] bytes)
        {
            string result = "";
            foreach (byte b in bytes) result += b.ToString("x2");
            return result;
        }

        private static String GetFileHash(byte[] file)
        {
            SHA256 Sha256 = SHA256.Create();
            return BytesToString(Sha256.ComputeHash(file));
        }

        public AdaptionDescriptor GetDescriptor (byte[] file)
        {
            AdaptionDescriptor descriptor = null;
            String fileHash = GetFileHash(file);

            if (!CacheMap.TryGetValue(fileHash, out descriptor))
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
