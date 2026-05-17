using System.Globalization;

namespace SafeRideKids.Motorista.Maui.Resources.Styles;

// Converters basicos usados nos XAMLs. Mantidos juntos para reduzir ruido.

public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

public sealed class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Mapeia chave de status (definida em CheckInHistoryItem.StatusKey) para uma Color
// usando recursos cadastrados em Colors.xaml. Centralizado para facilitar tematizacao.
public sealed class StatusKeyToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string ?? "StatusUnknown";
        var resourceKey = key switch
        {
            "StatusApproved" => "StatusApprovedColor",
            "StatusFallbackOk" => "StatusFallbackOkColor",
            "StatusRejected" => "StatusRejectedColor",
            "StatusInconclusive" => "StatusInconclusiveColor",
            "StatusPendingSync" => "StatusPendingSyncColor",
            _ => "Gray400"
        };

        // TryGetValue percorre MergedDictionaries automaticamente em MAUI 8+.
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var color) == true && color is Color c)
        {
            return c;
        }
        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
