using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace Diplom
{
    internal class GradeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var grade = value?.ToString()?.Trim() ?? "";
            var param = parameter?.ToString() ?? "foreground";

            if (string.IsNullOrEmpty(grade))
            {
                if (param == "background")
                    return Brushes.Transparent;
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333"));
            }

            if (param == "background")
            {
                return grade switch
                {
                    "5" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9")),
                    "4" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0")),
                    "3" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF8E1")),
                    "2" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE")),
                    "H" or "h" or "Н" or "н" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E5F5")),
                    _ => Brushes.Transparent
                };
            }

            // foreground (по умолчанию)
            return grade switch
            {
                "5" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")),
                "4" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100")),
                "3" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9A825")),
                "2" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828")),
                "H" or "h" or "Н" or "н" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B1FA2")),
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
