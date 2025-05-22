using System;
using System.Windows;

public static class LocalizationHelper
{
    /// <summary>
    /// Retrieves a localized string from the current ResourceDictionary.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="args">Optional parameters for formatting.</param>
    /// <returns>The localized string, formatted if needed.</returns>
    public static string GetLocalizedString(string key, params object[] args)
    {
        if (Application.Current.Resources.Contains(key))
        {
            string localizedString = Application.Current.Resources[key] as string;
            return args.Length > 0 ? string.Format(localizedString, args) : localizedString;
        }
        return key; // Return the key if not found
    }

    /// <summary>
    /// Shows a MessageBox with a dynamically localized message.
    /// </summary>
    /// <param name="key">The resource key for the message.</param>
    /// <param name="titleKey">The resource key for the title.</param>
    /// <param name="button">MessageBox buttons (default: OK).</param>
    /// <param name="icon">MessageBox icon (default: Information).</param>
    /// <param name="args">Optional parameters for formatting.</param>
    public static void ShowLocalizedMessage(string key, string titleKey, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information, params object[] args)
    {
        string message = GetLocalizedString(key, args);
        string title = GetLocalizedString(titleKey);
        MessageBox.Show(message, title, button, icon);
    }
}
