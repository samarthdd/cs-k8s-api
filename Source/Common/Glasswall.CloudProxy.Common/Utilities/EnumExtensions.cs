using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace Glasswall.CloudProxy.Common.Utilities
{
    public static class EnumExtensions
    {
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
    }
}
