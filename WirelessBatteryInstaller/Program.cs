using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WirelessBatteryInstaller;

internal sealed record InstallerLocale(
    string Code,
    string DisplayName,
    string DialogTitle,
    string SelectLanguageLabel,
    string NextButtonText,
    string CancelButtonText,
    string DesktopShortcutQuestion,
    string YesText,
    string NoText,
    string FolderBrowserDescription,
    string SuccessMessageFormat,
    string ErrorMessageFormat,
    string UninstallTitle,
    string UninstallConfirmQuestion,
    string UninstallSuccessMessage,
    string UninstallShortcutName);

internal static class Program
{
    private const string EmbeddedExeResourceName = "WirelessBatteryMonitor.exe";
    private const string AppFolderName = "WirelessBatteryMonitor";
    private const string ExeFileName = "WirelessBatteryMonitor.exe";
    private const string UninstallerFileName = "Uninstall.exe";
    private const string LangFileName = "language.txt";
    private const string ShortcutFileName = "Wireless Battery Monitor.lnk";
    private const string UninstallRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WirelessBatteryMonitor";

    private static readonly IReadOnlyList<InstallerLocale> Locales = new[]
    {
        new InstallerLocale(
            "en",
            "English",
            "Wireless Battery Monitor — Setup",
            "Select installation language:",
            "Next",
            "Cancel",
            "Add a shortcut to the Desktop?",
            "Yes",
            "No",
            "Select installation folder for Wireless Battery Monitor",
            "Installation completed successfully!\n\nApplication file:\n{0}\n\nAdded to Start Menu: Yes\nDesktop shortcut: {1}",
            "Installation error:\n{0}",
            "Uninstall Wireless Battery Monitor",
            "Are you sure you want to uninstall Wireless Battery Monitor?",
            "Wireless Battery Monitor has been successfully uninstalled.",
            "Uninstall Wireless Battery Monitor.lnk"),
        new InstallerLocale(
            "ru",
            "Русский",
            "Установка Wireless Battery Monitor",
            "Выберите язык:",
            "Далее",
            "Отмена",
            "Добавить ярлык на рабочий стол?",
            "Да",
            "Нет",
            "Выберите папку для установки Wireless Battery Monitor",
            "Установка успешно завершена!\n\nФайл приложения:\n{0}\n\nДобавлено в меню «Пуск»: Да\nЯрлык на рабочем столе: {1}",
            "Ошибка при установке:\n{0}",
            "Удаление Wireless Battery Monitor",
            "Вы действительно хотите удалить Wireless Battery Monitor?",
            "Wireless Battery Monitor успешно удалён.",
            "Удалить Wireless Battery Monitor.lnk"),
        new InstallerLocale(
            "uk",
            "Українська",
            "Встановлення Wireless Battery Monitor",
            "Оберіть мову:",
            "Далі",
            "Скасувати",
            "Додати ярлик на робочий стіл?",
            "Так",
            "Ні",
            "Оберіть папку для встановлення Wireless Battery Monitor",
            "Встановлення успішно завершено!\n\nФайл програми:\n{0}\n\nДодано до меню «Пуск»: Так\nЯрлик на робочому столі: {1}",
            "Помилка під час встановлення:\n{0}",
            "Видалення Wireless Battery Monitor",
            "Ви дійсно бажаєте видалити Wireless Battery Monitor?",
            "Wireless Battery Monitor успішно видалено.",
            "Видалити Wireless Battery Monitor.lnk"),
        new InstallerLocale(
            "pl",
            "Polski",
            "Instalacja Wireless Battery Monitor",
            "Wybierz język:",
            "Dalej",
            "Anuluj",
            "Dodać skrót na pulpicie?",
            "Tak",
            "Nie",
            "Wybierz folder instalacji Wireless Battery Monitor",
            "Instalacja zakończona pomyślnie!\n\nPlik aplikacji:\n{0}\n\nDodano do menu Start: Tak\nSkrót na pulpicie: {1}",
            "Błąd podczas instalacji:\n{0}",
            "Odinstaluj Wireless Battery Monitor",
            "Czy na pewno chcesz odinstalować Wireless Battery Monitor?",
            "Wireless Battery Monitor został pomyślnie odinstalowany.",
            "Odinstaluj Wireless Battery Monitor.lnk"),
        new InstallerLocale(
            "de",
            "Deutsch",
            "Wireless Battery Monitor — Installation",
            "Sprache auswählen:",
            "Weiter",
            "Abbrechen",
            "Verknüpfung auf dem Desktop hinzufügen?",
            "Ja",
            "Nein",
            "Wählen Sie den Installationsordner für Wireless Battery Monitor",
            "Installation erfolgreich abgeschlossen!\n\nAnwendungsdatei:\n{0}\n\nZum Startmenü hinzugefügt: Ja\nDesktop-Verknüpfung: {1}",
            "Fehler bei der Installation:\n{0}",
            "Wireless Battery Monitor deinstallieren",
            "Möchten Sie Wireless Battery Monitor wirklich deinstallieren?",
            "Wireless Battery Monitor wurde erfolgreich deinstalliert.",
            "Wireless Battery Monitor deinstallieren.lnk"),
        new InstallerLocale(
            "be",
            "Беларуская",
            "Устаноўка Wireless Battery Monitor",
            "Абярыце мову:",
            "Далей",
            "Скасаваць",
            "Дадаць ярлык на працоўны стол?",
            "Так",
            "Не",
            "Абярыце папку для ўстаноўкі Wireless Battery Monitor",
            "Устаноўка паспяхова завершана!\n\nФайл праграмы:\n{0}\n\nДададзена ў меню «Пуск»: Так\nЯрлык на працоўным стале: {1}",
            "Памылка пры ўстаноўцы:\n{0}",
            "Выдаленне Wireless Battery Monitor",
            "Вы сапраўды жадаеце выдаліць Wireless Battery Monitor?",
            "Wireless Battery Monitor паспяхова выдалены.",
            "Выдаліць Wireless Battery Monitor.lnk")
    };

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            RunUninstall();
            return;
        }

        // 1. Language selection question
        InstallerLocale? selectedLocale = ShowLanguageSelectionDialog();
        if (selectedLocale == null)
        {
            return;
        }

        // 2. Desktop shortcut question (default: No / Button2)
        DialogResult desktopShortcutResult = MessageBox.Show(
            selectedLocale.DesktopShortcutQuestion,
            selectedLocale.DialogTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        bool createDesktopShortcut = desktopShortcutResult == DialogResult.Yes;

        // 3. Installation folder selection
        using var folderDialog = new FolderBrowserDialog
        {
            Description = selectedLocale.FolderBrowserDescription,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        if (folderDialog.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
        {
            return;
        }

        try
        {
            StopRunningMonitorInstances();

            string targetDirectory = Path.Combine(folderDialog.SelectedPath, AppFolderName);
            Directory.CreateDirectory(targetDirectory);

            string targetExePath = Path.Combine(targetDirectory, ExeFileName);
            ExtractEmbeddedMonitor(targetExePath);

            string langFilePath = Path.Combine(targetDirectory, LangFileName);
            File.WriteAllText(langFilePath, selectedLocale.Code);

            string uninstallerPath = Path.Combine(targetDirectory, UninstallerFileName);
            string? currentProcessPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(currentProcessPath) && File.Exists(currentProcessPath))
            {
                if (!string.Equals(
                        Path.GetFullPath(currentProcessPath),
                        Path.GetFullPath(uninstallerPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(currentProcessPath, uninstallerPath, overwrite: true);
                }
            }

            if (createDesktopShortcut)
            {
                string desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopShortcutPath = Path.Combine(desktopDirectory, ShortcutFileName);
                CreateShortcut(desktopShortcutPath, targetExePath, string.Empty, targetDirectory, targetExePath);
            }

            string startMenuProgramsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string startMenuShortcutPath = Path.Combine(startMenuProgramsDir, ShortcutFileName);
            CreateShortcut(startMenuShortcutPath, targetExePath, string.Empty, targetDirectory, targetExePath);

            RegisterUninstallerInWindows(targetDirectory, targetExePath, uninstallerPath);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExePath,
                    Arguments = $"--lang {selectedLocale.Code}",
                    WorkingDirectory = targetDirectory,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore if auto-start is blocked
            }

            string desktopStatus = createDesktopShortcut ? selectedLocale.YesText : selectedLocale.NoText;
            MessageBox.Show(
                string.Format(selectedLocale.SuccessMessageFormat, targetExePath, desktopStatus),
                selectedLocale.DialogTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(selectedLocale.ErrorMessageFormat, ex.Message),
                selectedLocale.DialogTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void RegisterUninstallerInWindows(string installDir, string targetExePath, string uninstallerPath)
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(UninstallRegistrySubKey, writable: true);
        if (key == null)
        {
            return;
        }

        string uninstallCmd = $"\"{uninstallerPath}\" --uninstall";
        key.SetValue("DisplayName", "Wireless Battery Monitor", RegistryValueKind.String);
        key.SetValue("DisplayIcon", $"{targetExePath},0", RegistryValueKind.String);
        key.SetValue("DisplayVersion", "1.0.0", RegistryValueKind.String);
        key.SetValue("Publisher", "WirelessBatteryMonitor", RegistryValueKind.String);
        key.SetValue("InstallLocation", installDir, RegistryValueKind.String);
        key.SetValue("UninstallString", uninstallCmd, RegistryValueKind.String);
        key.SetValue("QuietUninstallString", uninstallCmd, RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void RunUninstall()
    {
        string installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        try
        {
            using RegistryKey? regKey = Registry.CurrentUser.OpenSubKey(UninstallRegistrySubKey, writable: false);
            if (regKey?.GetValue("InstallLocation") is string regInstallDir &&
                !string.IsNullOrWhiteSpace(regInstallDir) &&
                Directory.Exists(regInstallDir))
            {
                installDir = regInstallDir;
            }
        }
        catch
        {
            // Fallback to AppContext.BaseDirectory
        }

        InstallerLocale locale = ResolveInstalledLocale(installDir);

        DialogResult confirm = MessageBox.Show(
            locale.UninstallConfirmQuestion,
            locale.UninstallTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        StopRunningMonitorInstances();

        try
        {
            string startMenuProgramsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string startMenuShortcut = Path.Combine(startMenuProgramsDir, ShortcutFileName);
            if (File.Exists(startMenuShortcut))
            {
                File.Delete(startMenuShortcut);
            }

            foreach (InstallerLocale loc in Locales)
            {
                string uninstShortcut = Path.Combine(startMenuProgramsDir, loc.UninstallShortcutName);
                if (File.Exists(uninstShortcut))
                {
                    File.Delete(uninstShortcut);
                }
            }
        }
        catch
        {
            // Ignore shortcut removal error
        }

        try
        {
            string desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string desktopShortcut = Path.Combine(desktopDirectory, ShortcutFileName);
            if (File.Exists(desktopShortcut))
            {
                File.Delete(desktopShortcut);
            }
        }
        catch
        {
            // Ignore desktop shortcut removal error
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistrySubKey, throwOnMissingSubKey: false);
        }
        catch
        {
            // Ignore registry cleanup error
        }

        try
        {
            string monitorExe = Path.Combine(installDir, ExeFileName);
            if (File.Exists(monitorExe))
            {
                File.Delete(monitorExe);
            }

            string langFile = Path.Combine(installDir, LangFileName);
            if (File.Exists(langFile))
            {
                File.Delete(langFile);
            }
        }
        catch
        {
            // Will be cleaned up by delayed directory removal
        }

        MessageBox.Show(
            locale.UninstallSuccessMessage,
            locale.UninstallTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        try
        {
            if (Directory.Exists(installDir) &&
                installDir.EndsWith(AppFolderName, StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{installDir}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }
        catch
        {
            // Ignore delayed cleanup error
        }
    }

    private static InstallerLocale ResolveInstalledLocale(string installDir)
    {
        try
        {
            string langFile = Path.Combine(installDir, LangFileName);
            if (File.Exists(langFile))
            {
                string code = File.ReadAllText(langFile).Trim();
                InstallerLocale? matched = Locales.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    return matched;
                }
            }
        }
        catch
        {
            // Ignore read error
        }

        string osTwoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return Locales.FirstOrDefault(l => l.Code.Equals(osTwoLetter, StringComparison.OrdinalIgnoreCase))
            ?? Locales[1];
    }

    private static InstallerLocale? ShowLanguageSelectionDialog()
    {
        string osTwoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        int initialIndex = 1; // Default to Russian
        for (int i = 0; i < Locales.Count; i++)
        {
            if (Locales[i].Code.Equals(osTwoLetter, StringComparison.OrdinalIgnoreCase))
            {
                initialIndex = i;
                break;
            }
        }

        using var form = new Form
        {
            Width = 380,
            Height = 200,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false
        };

        var label = new Label
        {
            Left = 20,
            Top = 20,
            Width = 325,
            Height = 24,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular)
        };

        var combo = new ComboBox
        {
            Left = 20,
            Top = 50,
            Width = 325,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular)
        };

        foreach (InstallerLocale loc in Locales)
        {
            combo.Items.Add(loc.DisplayName);
        }

        var btnOk = new Button
        {
            Left = 155,
            Top = 105,
            Width = 90,
            Height = 32,
            DialogResult = DialogResult.OK,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
        };

        var btnCancel = new Button
        {
            Left = 255,
            Top = 105,
            Width = 90,
            Height = 32,
            DialogResult = DialogResult.Cancel,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
        };

        void ApplySelectedLocaleUI()
        {
            int idx = combo.SelectedIndex >= 0 ? combo.SelectedIndex : initialIndex;
            InstallerLocale current = Locales[idx];
            form.Text = current.DialogTitle;
            label.Text = current.SelectLanguageLabel;
            btnOk.Text = current.NextButtonText;
            btnCancel.Text = current.CancelButtonText;
        }

        combo.SelectedIndexChanged += (_, _) => ApplySelectedLocaleUI();
        combo.SelectedIndex = initialIndex;
        ApplySelectedLocaleUI();

        form.Controls.Add(label);
        form.Controls.Add(combo);
        form.Controls.Add(btnOk);
        form.Controls.Add(btnCancel);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog() == DialogResult.OK && combo.SelectedIndex >= 0)
        {
            return Locales[combo.SelectedIndex];
        }

        return null;
    }

    private static void StopRunningMonitorInstances()
    {
        try
        {
            foreach (Process proc in Process.GetProcessesByName("WirelessBatteryMonitor"))
            {
                using (proc)
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                    catch
                    {
                        // Ignore if already exited or inaccessible
                    }
                }
            }
        }
        catch
        {
            // Ignore process enumeration errors
        }
    }

    private static void ExtractEmbeddedMonitor(string destinationPath)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.Equals(EmbeddedExeResourceName, StringComparison.OrdinalIgnoreCase)
                              || n.EndsWith(EmbeddedExeResourceName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            throw new FileNotFoundException($"Embedded resource '{EmbeddedExeResourceName}' not found.");
        }

        using Stream resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Failed to open resource stream '{resourceName}'.");

        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        resourceStream.CopyTo(fileStream);
    }

    private static void CreateShortcut(
        string shortcutPath,
        string targetExePath,
        string arguments,
        string workingDirectory,
        string iconExePath)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Failed to get COM type WScript.Shell.");

        object shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Failed to create WScript.Shell instance.");

        try
        {
            object? shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });

            if (shortcut == null)
            {
                throw new InvalidOperationException("Failed to create shortcut via WScript.Shell.");
            }

            try
            {
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExePath });
                if (!string.IsNullOrEmpty(arguments))
                {
                    shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                }
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Wireless Battery Monitor" });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { $"{iconExePath},0" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, Array.Empty<object>());
            }
            finally
            {
                if (Marshal.IsComObject(shortcut))
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }
        }
        finally
        {
            if (Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
