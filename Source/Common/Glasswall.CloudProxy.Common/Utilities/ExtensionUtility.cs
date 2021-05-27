using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Glasswall.CloudProxy.Common.Utilities
{
    public static class ExtensionUtility
    {
        private static readonly ILoggerFactory _loggerFactory;
        private static readonly ILogger _logger;

        static ExtensionUtility()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            _logger = _loggerFactory.CreateLogger("ExtensionUtility");
        }

        public static string FormattedJson(this string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return rawJson;
            }

            try
            {
                return JToken.Parse(rawJson).ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Exception while formatting JSON {rawJson} and exception detail {ex.Message}");
            }
            return rawJson;
        }

        public static string GetDescription<TEnum>(this TEnum e) where TEnum : IConvertible
        {
            if (e is Enum)
            {
                Type eType = e.GetType();
                Array enumValues = Enum.GetValues(eType);

                foreach (int val in enumValues)
                {
                    if (val == e.ToInt32(CultureInfo.InvariantCulture))
                    {
                        System.Reflection.MemberInfo[] memInfo = eType.GetMember(eType.GetEnumName(val));
                        if (memInfo[0]
                            .GetCustomAttributes(typeof(DescriptionAttribute), false)
                            .FirstOrDefault() is DescriptionAttribute descriptionAttribute)
                        {
                            return descriptionAttribute.Description;
                        }
                    }
                }
            }

            return null;
        }

        public static T XmlStringToObject<T>(this string xml)
        {
            T retValue = default;
            if (string.IsNullOrWhiteSpace(xml))
            {
                return retValue;
            }

            try
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
                using StringReader stream = new StringReader(xml);
                retValue = (T)xmlSerializer.Deserialize(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while convert XML string to Object {ex}");
            }
            return retValue;
        }

        public static string XmlStringToJson(this string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return xml;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                return JsonConvert.SerializeXmlNode(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while convert XML string to Json {ex}");
                return xml;
            }
        }
    }
}
