using System;
using System.Globalization;
using System.Windows.Data;

namespace SimPc
{
    public class InBuildToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((bool)value) ? "✓ В сборке" : "+ Добавить";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}