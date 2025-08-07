using System;
using System.Globalization;
using System.Windows.Data;

namespace EphemeralBrowser.UI.Converters
{
    public class EnumToBoolConverter : IValueConverter
    {
        public static EnumToBoolConverter Instance { get; } = new EnumToBoolConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return value.ToString()?.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase) == true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter == null || value is not bool boolValue || !boolValue)
                return Binding.DoNothing;

            try
            {
                return Enum.Parse(targetType, parameter.ToString()!);
            }
            catch (ArgumentException)
            {
                return Binding.DoNothing;
            }
        }
    }
}
