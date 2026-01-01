using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NovviaERP.WPF.Converters
{
    /// <summary>
    /// Bool zu "Ja"/"Nein" Converter
    /// </summary>
    public class BoolToYesNoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? "Ja" : "Nein";
            return "Nein";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Bool zu Farbe Converter (Grün=true, Grau=false, mit Invert-Option)
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool bVal && bVal;
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            if (invert) b = !b;

            return b ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Bool zu Visibility Converter
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Bestellstatus zu Text Converter (JTL-Wawi tLieferantenBestellung.nStatus)
    /// </summary>
    public class BestellStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                return status switch
                {
                    0 => "Offen",
                    5 => "Freigegeben",
                    20 => "In Bearbeitung",
                    30 => "Teilgeliefert",
                    500 => "Abgeschlossen",
                    700 => "Dropshipping",
                    -1 => "Storniert",
                    _ => $"Status {status}"
                };
            }
            return "Unbekannt";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Integer (0/1) zu Visibility Converter
    /// </summary>
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i && i > 0)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Null zu Collapsed Converter - zeigt Element nur an wenn Wert nicht null ist
    /// </summary>
    public class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
