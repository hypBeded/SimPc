using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SimPc
{
    public class InBuildToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((bool)value) ? new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C)) : new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}