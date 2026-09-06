using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TremauxLock.App.Converters;

/// <summary>
/// Converte booleano para Visibility, com suporte a parâmetro "Inverse", "Not" ou "true"
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolVal = value is bool b && b;
        bool invert = parameter is string s && (s.Equals("Inverse", StringComparison.OrdinalIgnoreCase) || s.Equals("Not", StringComparison.OrdinalIgnoreCase) || s.Equals("True", StringComparison.OrdinalIgnoreCase));

        if (invert)
        {
            boolVal = !boolVal;
        }

        return boolVal ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

/// <summary>
/// Retorna Visibility.Visible se o objeto/string não for nulo nem vazio
/// </summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasValue = value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => true
        };

        bool invert = parameter is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
        if (invert) hasValue = !hasValue;

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
