using System.ComponentModel;
using ElectionPageCreateTool.Attributes;

namespace ElectionPageCreateTool.Extensions
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute =
                (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
            return attribute != null ? attribute.Description : value.ToString();
        }


        public static int GetShugiinKuwariCount(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (ShugiinKuwariAttribute)Attribute.GetCustomAttribute(field, typeof(ShugiinKuwariAttribute));
            return attribute?.Kuwari ?? 0;
        }


        public static string GetShugiinKuwariStr(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (ShugiinKuwariAttribute)Attribute.GetCustomAttribute(field, typeof(ShugiinKuwariAttribute));
            return attribute?.Name ?? "";
        }


        public static string GetAbbreviation(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (AbbreviationAttribute)Attribute.GetCustomAttribute(field, typeof(AbbreviationAttribute));
            return attribute != null ? attribute.Name : value.ToString();
        }

        public static string GetAlphabetName(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (AlphabetNameAttribute)Attribute.GetCustomAttribute(field, typeof(AlphabetNameAttribute));
            return attribute != null ? attribute.Name : value.ToString();
        }
    }
}