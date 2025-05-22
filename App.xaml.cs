using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
using System.Windows.Threading;
using System;
namespace friction_tester
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Catch exceptions on the UI thread
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            // Catch exceptions on background threads
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            base.OnStartup(e);

            // Determine the log path (must match Logger.LogFilePath)
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApplicationLog.txt");
            string startupLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_ui_error.log");
            // If the file exists, clear it
            if (File.Exists(logPath))
            {
                File.WriteAllText(logPath, string.Empty);
            }
            if (File.Exists(startupLogPath))
            {
                File.WriteAllText(startupLogPath, string.Empty);
            }
            // Force a DB check/migrate on launch
            using var ctx = new TestResultsContext();
            if (!ctx.Database.CanConnect())
            {
                MessageBox.Show("Unable to connect to the database. Check service, firewall, and pg_hba.conf.");
                //this.Shutdown();
                //return;
            }
            // Load Config
            ConfigManager.LoadConfig();

            // If no language is set, default to Chinese
            if (string.IsNullOrEmpty(ConfigManager.Config.SelectedLanguage))
            {
                //ConfigManager.Config.SelectedLanguage = "zh-CN";
                //ConfigManager.SaveConfig(ConfigManager.Config);
            }

            // Apply the language setting
            LanguageManager.ChangeLanguage(ConfigManager.Config.SelectedLanguage);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            File.WriteAllText("startup_ui_error.log", e.Exception.ToString());
            MessageBox.Show($"Startup UI error:\n{e.Exception.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;  // prevent default crash dialog
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            File.WriteAllText("startup_domain_error.log", ex?.ToString() ?? "Unknown fatal error");
        }
    }

}
