using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SecureGate.Domain.Common
{
    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum enumValue)
        {
            // Bazadan kelgan noma'lum (enum'da mavjud bo'lmagan) qiymat raqam ko'rinishida
            // chiqib ketmasligi uchun.
            if (!Enum.IsDefined(enumValue.GetType(), enumValue))
                return "Noma'lum";

            var displayAttribute = enumValue.GetType()
                .GetMember(enumValue.ToString())
                .FirstOrDefault()?
                .GetCustomAttribute<DisplayAttribute>();

            return displayAttribute?.Name ?? enumValue.ToString();
        }
    }
}