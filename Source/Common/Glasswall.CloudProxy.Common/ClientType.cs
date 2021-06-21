using System.ComponentModel;

namespace Glasswall.CloudProxy.Common
{
    public enum ClientType
    {
        [Description(Constants.UserAgent.WEB_APP)]
        WEB_APP = 0,

        [Description(Constants.UserAgent.DESKTOP_APP)]
        DESKTOP_APP = 1,

        [Description(Constants.UserAgent.OTHERS)]
        OTHERS = 2
    }
}
