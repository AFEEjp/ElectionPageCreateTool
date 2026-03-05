using System.ComponentModel;
using ElectionPageCreateTool.Attributes;

namespace ElectionPageCreateTool.Extensions
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null) return value.ToString();

            var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attribute?.Description ?? value.ToString();
        }

        public static int GetShugiinKuwariCount(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null) return 0;

            var attribute = Attribute.GetCustomAttribute(field, typeof(ShugiinKuwariAttribute)) as ShugiinKuwariAttribute;
            return attribute?.Kuwari ?? 0;
        }

        public static string GetShugiinKuwariStr(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null) return string.Empty;

            var attribute = Attribute.GetCustomAttribute(field, typeof(ShugiinKuwariAttribute)) as ShugiinKuwariAttribute;
            return attribute?.Name ?? string.Empty;
        }

        public static string GetAbbreviation(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null) return value.ToString();

            var attribute = Attribute.GetCustomAttribute(field, typeof(AbbreviationAttribute)) as AbbreviationAttribute;
            return attribute?.Name ?? value.ToString();
        }

        public static string GetAlphabetName(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null) return value.ToString();

            var attribute = Attribute.GetCustomAttribute(field, typeof(AlphabetNameAttribute)) as AlphabetNameAttribute;
            return attribute?.Name ?? value.ToString();
        }
    }
}