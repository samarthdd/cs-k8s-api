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
        //private string name; // field
        //public string Name   // property
        //{
        //    get { return name; }   // get method
        //    set { name = value; }  // set method
        //}

        private Guid uuid; // field
        public Guid UUID   // property
        {
            get { return uuid; }   // get method
            set { uuid = value; }  // set method
        }
    }

    public sealed class AdaptionCache
    {
        ConcurrentDictionary<String, AdaptionDescriptor> CacheMap;

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

        private String GetFileHash(byte[] file)
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
                descriptor.UUID = Guid.NewGuid();
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
