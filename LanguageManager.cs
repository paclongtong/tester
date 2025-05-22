using System.Globalization;
using System.Threading;
using System.Windows;
using friction_tester;

public static class LanguageManager
{
    public static void ChangeLanguage(string cultureCode)
    {
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureCode);
        Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureCode);

        // Load the proper ResourceDictionary
        ResourceDictionary dict = new ResourceDictionary();
        switch (cultureCode)
        {
            case "zh-CN":
                dict.Source = new Uri("Resources/Strings.zh-CN.xaml", UriKind.Relative);
                break;
            case "ru-RU":
                dict.Source = new Uri("Resources/Strings.ru-RU.xaml", UriKind.Relative);
                break;
            case "fr-FR":
                dict.Source = new Uri("Resources/Strings.fr-FR.xaml", UriKind.Relative);
                break;
            case "en-US":
                dict.Source = new Uri("Resources/Strings.en-US.xaml", UriKind.Relative);
                break;
            default:
                dict.Source = new Uri("Resources/Strings.en-US.xaml", UriKind.Relative);
                break;
        }

        // Clear existing dictionaries and add the new one
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dict);

        foreach (Window window in Application.Current.Windows)
        {
            window.Resources.MergedDictionaries.Clear();
            window.Resources.MergedDictionaries.Add(dict);
        }

        // 🔥 Update all open windows with the new language
        foreach (Window window in Application.Current.Windows)
        {
            window.Language = System.Windows.Markup.XmlLanguage.GetLanguage(cultureCode);
        }
        //// Example usage
        //MessageBox.Show(Application.Current.Resources["HardLimitEnable"].ToString(),
        //                "Info",
        //                MessageBoxButton.OK,
        //                MessageBoxImage.Information);
    }
}

