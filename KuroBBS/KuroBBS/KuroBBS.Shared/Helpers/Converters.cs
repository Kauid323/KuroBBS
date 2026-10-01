using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace KuroBBS.Helpers
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && (bool)value)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && (bool)value)
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility && (Visibility)value != Visibility.Visible;
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool)
            {
                return !((bool)value);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool)
            {
                return !((bool)value);
            }
            return false;
        }
    }

    public class StringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return "";
            if (parameter != null)
            {
                return string.Format(parameter.ToString(), value);
            }
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ImageCacheConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return null;
            string url = value.ToString();
            if (string.IsNullOrWhiteSpace(url)) return null;

            int decodeWidth = 720;
            if (parameter != null)
            {
                int customWidth;
                if (int.TryParse(parameter.ToString(), out customWidth) && customWidth > 0)
                {
                    decodeWidth = customWidth;
                }
            }

            return KuroBBS.Services.KuroImageCache.Instance.GetImageSource(url, decodeWidth);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToActiveBrushConverter : IValueConverter
    {
        private static readonly Windows.UI.Xaml.Media.SolidColorBrush ActiveBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 163, 255));
        private static readonly Windows.UI.Xaml.Media.SolidColorBrush InactiveBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 142, 142, 147));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && (bool)value)
            {
                return ActiveBrush;
            }
            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && (bool)value)
            {
                return Windows.UI.Text.FontWeights.Bold;
            }
            return Windows.UI.Text.FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return Visibility.Collapsed;
            if (value is string && string.IsNullOrWhiteSpace((string)value)) return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return Visibility.Visible;
            if (value is string && string.IsNullOrWhiteSpace((string)value)) return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToSymbolIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return new Windows.UI.Xaml.Controls.SymbolIcon(Windows.UI.Xaml.Controls.Symbol.Accept);
            string name = value.ToString();
            Windows.UI.Xaml.Controls.Symbol symbol;
            if (Enum.TryParse<Windows.UI.Xaml.Controls.Symbol>(name, true, out symbol))
            {
                return new Windows.UI.Xaml.Controls.SymbolIcon(symbol);
            }
            return new Windows.UI.Xaml.Controls.SymbolIcon(Windows.UI.Xaml.Controls.Symbol.Accept);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class FollowStateToBackgroundConverter : IValueConverter
    {
        private static readonly Windows.UI.Xaml.Media.SolidColorBrush FollowedBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 40, 40, 52));
        private static readonly Windows.UI.Xaml.Media.SolidColorBrush UnfollowedBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 163, 255));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && (bool)value)
            {
                return FollowedBrush;
            }
            return UnfollowedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class FollowStateToForegroundConverter : IValueConverter
    {
        private static readonly Windows.UI.Xaml.Media.SolidColorBrush FollowedBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 176, 176, 192));
        private static readonly Windows.UI.Xaml.Media.SolidColorBrush UnfollowedBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && (bool)value)
            {
                return FollowedBrush;
            }
            return UnfollowedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToAccentBrushConverter : IValueConverter
    {
        private static readonly Windows.UI.Xaml.Media.SolidColorBrush SelectedBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 163, 255));
        private static readonly Windows.UI.Xaml.Media.SolidColorBrush UnselectedBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 26, 26, 34));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && (bool)value)
            {
                return SelectedBrush;
            }
            return UnselectedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToTextBrushConverter : IValueConverter
    {
        private static readonly Windows.UI.Xaml.Media.SolidColorBrush SelectedBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        private static readonly Windows.UI.Xaml.Media.SolidColorBrush UnselectedBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 142, 142, 147));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && (bool)value)
            {
                return SelectedBrush;
            }
            return UnselectedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

