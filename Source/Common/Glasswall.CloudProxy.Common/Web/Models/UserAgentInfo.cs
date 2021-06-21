using Glasswall.CloudProxy.Common.Utilities;
using UAParser;

namespace Glasswall.CloudProxy.Common.Web.Models
{
    public class UserAgentInfo
    {
        public UserAgentInfo(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                ClientType = ClientType.OTHERS;
                ClientTypeString = ClientType.GetDescription();
                return;
            }

            Parser uaParser = Parser.GetDefault();
            ClientInfo clientInfo = uaParser.Parse(userAgent);
            ClientInfo = clientInfo;
            switch (clientInfo.UA.Family.ToLower())
            {
                case Constants.UserAgent.ELECTRON_FAMILY:
                    {
                        ClientType = ClientType.DESKTOP_APP;
                        break;
                    }
                case Constants.UserAgent.OTHER_FAMILY:
                    {
                        ClientType = ClientType.OTHERS;
                        break;
                    }
                default:
                    {
                        ClientType = ClientType.WEB_APP;
                        break;
                    }
            }
            ClientTypeString = ClientType.GetDescription();
        }

        public ClientInfo ClientInfo { get; }
        public ClientType ClientType { get; }
        public string ClientTypeString { get; }
    }
}
