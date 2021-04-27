using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Glasswall.CloudProxy.Common.Utilities
{
    public static class StringExtensions
    {
        private static readonly ILoggerFactory _loggerFactory;
        private static readonly ILogger _logger;

        static StringExtensions()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            _logger = _loggerFactory.CreateLogger("StringExtensions");
        }

        public static string FormattedJson(this string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return rawJson;
            }

            try
            {
                return JToken.Parse(rawJson).ToString(Formatting.Indented);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Exception while formatting JSON {rawJson} and exception detail {ex.Message}");
            }
            return rawJson;
        }
    }
}
