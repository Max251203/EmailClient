using EmailClient.Core.Models;
using System.Globalization;
using System.Windows.Data;

namespace EmailClient.UI.Converters
{
    public class LoadingTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isLoading && isLoading ? "Please wait..." : "Sign In";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProviderToDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EmailProvider provider)
            {
                return provider switch
                {   
                    EmailProvider.MailRu => "Mail.ru",
                    EmailProvider.GstuMail => "GSTU Mail",
                    EmailProvider.Custom => "Custom Server",
                    _ => string.Empty
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}