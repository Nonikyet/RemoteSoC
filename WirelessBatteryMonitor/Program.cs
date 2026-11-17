using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Power;
using Windows.Storage.Streams;

namespace WirelessBatteryMonitor;

internal static class Program
{
    private const string MutexName = @"Global\WirelessBatteryMonitor_SingleInstance_Mutex";

    [STAThread]
    private static void Main(string[] args)
    {
        string lang = Localization.LoadInitialLanguage(args);
        Localization.CurrentLanguage = lang;

        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                Localization.Get.AlreadyRunning,
                "Wireless Battery Monitor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new BatteryTrayApplicationContext());
    }
}

internal sealed record Strings(
    string Code,
    string DisplayName,
    string AlreadyRunning,
    string Loading,
    string PollingDevices,
    string NoDevicesTooltip,
    string NoDevicesMenu,
    string SeparateIconsMode,
    string LanguageMenu,
    string RefreshNow,
    string Exit,
    string PollError,
    string FallbackDevice);

internal static class Localization
{
    private const string LangFileName = "language.txt";

    public static readonly IReadOnlyList<Strings> AllLanguages = new[]
    {
        new Strings(
            "en", "English",
            "WirelessBatteryMonitor is already running in the system tray.",
            "Wireless Battery Monitor: loading...",
            "Polling devices...",
            "No wireless devices with battery found",
            "No devices found",
            "Separate icon and % elements",
            "Language",
            "Refresh now",
            "Exit",
            "Polling error",
            "Wireless device"),
        new Strings(
            "ru", "Русский",
            "WirelessBatteryMonitor уже запущен в системном трее.",
            "Wireless Battery Monitor: загрузка...",
            "Опрос устройств...",
            "Беспроводные устройства с батареей не найдены",
            "Устройства не найдены",
            "Иконка и % отдельными элементами",
            "Язык",
            "Обновить сейчас",
            "Выход",
            "Ошибка опроса",
            "Беспроводное устройство"),
        new Strings(
            "uk", "Українська",
            "WirelessBatteryMonitor вже запущено в системному треї.",
            "Wireless Battery Monitor: завантаження...",
            "Опитування пристроїв...",
            "Бездротові пристрої з батареєю не знайдені",
            "Пристрої не знайдені",
            "Іконка та % окремими елементами",
            "Мова",
            "Оновити зараз",
            "Вихід",
            "Помилка опитування",
            "Бездротовий пристрій"),
        new Strings(
            "pl", "Polski",
            "WirelessBatteryMonitor jest już uruchomiony w zasobniku systemowym.",
            "Wireless Battery Monitor: ładowanie...",
            "Wyszukiwanie urządzeń...",
            "Nie znaleziono urządzeń bezprzewodowych z baterią",
            "Nie znaleziono urządzeń",
            "Ikona i % jako osobne elementy",
            "Język",
            "Odśwież teraz",
            "Wyjdź",
            "Błąd odczytu",
            "Urządzenie bezprzewodowe"),
        new Strings(
            "de", "Deutsch",
            "WirelessBatteryMonitor läuft bereits im System-Tray.",
            "Wireless Battery Monitor: wird geladen...",
            "Geräte werden abgefragt...",
            "Keine kabellosen Geräte mit Akku gefunden",
            "Keine Geräte gefunden",
            "Symbol und % als separate Elemente",
            "Sprache",
            "Jetzt aktualisieren",
            "Beenden",
            "Abfragefehler",
            "Kabelloses Gerät"),
        new Strings(
            "be", "Беларуская",
            "WirelessBatteryMonitor ужо запушчаны ў сістэмным трэі.",
            "Wireless Battery Monitor: загрузка...",
            "Апытанне прылад...",
            "Бесправадныя прылады з батарэяй не знойдзены",
            "Прылады не знойдзены",
            "Іконка і % асобнымі элементамі",
            "Мова",
            "Абнавіць зараз",
            "Выхад",
            "Памылка апытання",
            "Бесправадная прылада")
    };

    public static string CurrentLanguage { get; set; } = "ru";

    public static Strings Get =>
        AllLanguages.FirstOrDefault(l => l.Code.Equals(CurrentLanguage, StringComparison.OrdinalIgnoreCase))
        ?? AllLanguages[1];

    public static string LoadInitialLanguage(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--lang", StringComparison.OrdinalIgnoreCase))
            {
                string candidate = args[i + 1].Trim().ToLowerInvariant();
                if (IsSupported(candidate))
                {
                    SaveLanguage(candidate);
                    return candidate;
                }
            }
        }

        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, LangFileName);
            if (File.Exists(path))
            {
                string saved = File.ReadAllText(path).Trim().ToLowerInvariant();
                if (IsSupported(saved))
                {
                    return saved;
                }
            }
        }
        catch
        {
            // Ignore file read errors
        }

        string osLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return IsSupported(osLang) ? osLang : "ru";
    }

    public static void SaveLanguage(string code)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, LangFileName);
            File.WriteAllText(path, code);
        }
        catch
        {
            // Ignore file write errors
        }
    }

    private static bool IsSupported(string code) =>
        AllLanguages.Any(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
}

internal enum DeviceKind
{
    Headphones,
    Mouse,
    Keyboard,
    Gamepad,
    Battery
}

internal sealed record DeviceBatteryInfo(
    string Name,
    byte BatteryPercent,
    string Source,
    Guid? ContainerId,
    DeviceKind Kind);

internal sealed record ContainerMetadata(string Name, string IconPath, string Category);

internal sealed class BatteryTrayApplicationContext : ApplicationContext
{
    private const string PropBatteryLifePercent = "System.Devices.BatteryLifePercent";
    private const string PropBatteryPlusCharging = "System.Devices.BatteryPlusCharging";
    private const string PropBatteryPlusChargingText = "System.Devices.BatteryPlusChargingText";
    private const string PropBluetoothBattery = "{104ea319-6ee2-4701-bd47-8ddbf425bbe5} 2";
    private const string PropBleConnectedState = "{995ef0b0-7eb3-4a8b-b9ce-068bb3f4af69} 4";
    private const string PropContainerId = "System.Devices.ContainerId";
    private const string PropAepContainerId = "System.Devices.Aep.ContainerId";
    private const string PropConnected = "System.Devices.Connected";
    private const string PropPresent = "System.Devices.Present";
    private const string PropIcon = "System.Devices.Icon";
    private const string PropGlyphIcon = "System.Devices.GlyphIcon";
    private const string PropPrimaryCategory = "System.Devices.PrimaryCategory";
    private const ushort BatteryServiceShortId = 0x180F;

    private static readonly Guid SystemRootContainerId = new("00000000-0000-0000-ffff-ffffffffffff");

    private readonly List<NotifyIcon> _trayIcons = new();
    private readonly ContextMenuStrip _contextMenu;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private IReadOnlyList<DeviceBatteryInfo> _lastDevices = Array.Empty<DeviceBatteryInfo>();
    private bool _separateGlyphAndPercent = true;
    private bool _isRefreshing;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public BatteryTrayApplicationContext()
    {
        _contextMenu = new ContextMenuStrip();

        BuildContextMenu(_lastDevices, isInitial: true);
        SyncTrayIcons(_lastDevices, isInitial: true);

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 60_000
        };
        _refreshTimer.Tick += async (_, _) => await RefreshDevicesAsync();
        _refreshTimer.Start();

        _ = RefreshDevicesAsync();
    }

    private void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && sender is NotifyIcon clickedIcon)
        {
            MethodInfo? showContextMenu = typeof(NotifyIcon).GetMethod(
                "ShowContextMenu",
                BindingFlags.Instance | BindingFlags.NonPublic);
            showContextMenu?.Invoke(clickedIcon, null);
        }
    }

    private async Task RefreshDevicesAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            List<DeviceBatteryInfo> devices = await Task.Run(QueryAllWirelessBatteriesAsync);
            _lastDevices = devices;
            UpdateTrayUI(devices);
        }
        catch (Exception ex)
        {
            foreach (NotifyIcon ni in _trayIcons)
            {
                SetTooltipSafe(ni, $"{Localization.Get.PollError}: {ex.Message}");
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static async Task<List<DeviceBatteryInfo>> QueryAllWirelessBatteriesAsync()
    {
        var results = new List<DeviceBatteryInfo>();
        var seenContainers = new HashSet<Guid>();
        string fallbackDevName = Localization.Get.FallbackDevice;

        var connectedContainers = new Dictionary<Guid, ContainerMetadata>();
        try
        {
            DeviceInformationCollection containers = await DeviceInformation.FindAllAsync(
                "System.Devices.Connected:=System.StructuredQueryType.Boolean#True",
                new[] { "System.ItemNameDisplay", PropConnected, PropIcon, PropGlyphIcon, PropPrimaryCategory },
                DeviceInformationKind.DeviceContainer);

            foreach (DeviceInformation container in containers)
            {
                if (Guid.TryParse(container.Id, out Guid cid) && cid != Guid.Empty && cid != SystemRootContainerId)
                {
                    string cName = container.Name?.Trim() ?? string.Empty;
                    string iconPath = GetStringProperty(container.Properties, PropIcon)
                                   ?? GetStringProperty(container.Properties, PropGlyphIcon)
                                   ?? string.Empty;
                    string category = GetStringProperty(container.Properties, PropPrimaryCategory) ?? string.Empty;
                    connectedContainers[cid] = new ContainerMetadata(cName, iconPath, category);
                }
            }
        }
        catch
        {
            // Continue even if container query fails
        }

        // 1. Bluetooth LE devices via GATT Battery Service (0x180F) or BatteryLevel characteristic (0x2A19)
        try
        {
            string bleSelector = BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
            string[] requestedBleProps = { PropAepContainerId, PropContainerId, PropIcon, PropGlyphIcon };
            DeviceInformationCollection bleDevices = await DeviceInformation.FindAllAsync(bleSelector, requestedBleProps);

            foreach (DeviceInformation devInfo in bleDevices)
            {
                try
                {
                    Guid? containerId = TryGetContainerId(devInfo.Properties);
                    if (containerId.HasValue && seenContainers.Contains(containerId.Value))
                    {
                        continue;
                    }

                    using BluetoothLEDevice? bleDevice = await BluetoothLEDevice.FromIdAsync(devInfo.Id);
                    if (bleDevice == null || bleDevice.ConnectionStatus != BluetoothConnectionStatus.Connected)
                    {
                        continue;
                    }

                    if (!containerId.HasValue && bleDevice.DeviceInformation != null)
                    {
                        containerId = TryGetContainerId(bleDevice.DeviceInformation.Properties);
                    }

                    byte? batteryLevel = await TryReadBleBatteryLevelAsync(bleDevice);
                    if (batteryLevel.HasValue)
                    {
                        string rawName = !string.IsNullOrWhiteSpace(bleDevice.Name) ? bleDevice.Name : devInfo.Name;
                        string name = ResolveFriendlyDeviceName(rawName, containerId, connectedContainers, fallbackDevName);
                        string iconHint = GetStringProperty(devInfo.Properties, PropIcon) ?? string.Empty;
                        DeviceKind kind = ClassifyDevice(name, devInfo.Id, iconHint, containerId, connectedContainers);

                        if (containerId.HasValue)
                        {
                            seenContainers.Add(containerId.Value);
                        }

                        results.Add(new DeviceBatteryInfo(name, batteryLevel.Value, "BLE", containerId, kind));
                    }
                }
                catch
                {
                    // Skip inaccessible BLE device
                }
            }
        }
        catch
        {
            // Ignore if Bluetooth adapter is disabled or unavailable
        }

        // 2. PnP / Bluetooth / 2.4 GHz / HID wireless devices via Windows.Devices.Enumeration
        try
        {
            var propsToRequest = new List<string>
            {
                PropBluetoothBattery,
                PropBatteryPlusCharging,
                PropBatteryPlusChargingText,
                PropBleConnectedState,
                PropContainerId,
                PropConnected,
                PropPresent,
                PropIcon
            };

            if (await IsPropertySupportedAsync(PropBatteryLifePercent))
            {
                propsToRequest.Add(PropBatteryLifePercent);
            }

            DeviceInformationCollection allDevices = await DeviceInformation.FindAllAsync(
                string.Empty,
                propsToRequest,
                DeviceInformationKind.Device);

            foreach (DeviceInformation devInfo in allDevices)
            {
                try
                {
                    if (!TryExtractDeviceBatteryPercent(devInfo.Properties, out byte percent))
                    {
                        continue;
                    }

                    if (devInfo.Properties.TryGetValue(PropPresent, out object? rawPresent) &&
                        rawPresent is bool isPresent && !isPresent)
                    {
                        continue;
                    }

                    Guid? containerId = TryGetContainerId(devInfo.Properties);

                    if (!IsDeviceActivelyConnected(devInfo.Properties, containerId, connectedContainers))
                    {
                        continue;
                    }

                    if (containerId.HasValue && containerId.Value != SystemRootContainerId)
                    {
                        if (!seenContainers.Add(containerId.Value))
                        {
                            continue;
                        }
                    }

                    string name = ResolveFriendlyDeviceName(devInfo.Name, containerId, connectedContainers, fallbackDevName);
                    string iconHint = GetStringProperty(devInfo.Properties, PropIcon) ?? string.Empty;
                    DeviceKind kind = ClassifyDevice(name, devInfo.Id, iconHint, containerId, connectedContainers);
                    string source = DetermineSourceLabel(devInfo.Id);

                    results.Add(new DeviceBatteryInfo(name, percent, source, containerId, kind));
                }
                catch
                {
                    // Skip malformed device entry
                }
            }
        }
        catch
        {
            // Ignore enumeration failure
        }

        // 3. HID / Dongle devices exposing Windows.Devices.Power.Battery
        try
        {
            string batterySelector = Battery.GetDeviceSelector();
            DeviceInformationCollection batteryDevices = await DeviceInformation.FindAllAsync(
                batterySelector,
                new[] { PropContainerId, "System.ItemNameDisplay" });

            foreach (DeviceInformation batInfo in batteryDevices)
            {
                try
                {
                    Guid? containerId = TryGetContainerId(batInfo.Properties);
                    if (containerId.HasValue && containerId.Value != SystemRootContainerId && seenContainers.Contains(containerId.Value))
                    {
                        continue;
                    }

                    Battery? battery = await Battery.FromIdAsync(batInfo.Id);
                    BatteryReport? report = battery?.GetReport();
                    if (report?.FullChargeCapacityInMilliwattHours is > 0 &&
                        report.RemainingCapacityInMilliwattHours is >= 0)
                    {
                        int pct = (int)Math.Round(
                            report.RemainingCapacityInMilliwattHours.Value * 100.0 /
                            report.FullChargeCapacityInMilliwattHours.Value);
                        pct = Math.Clamp(pct, 0, 100);

                        if (containerId.HasValue && containerId.Value != SystemRootContainerId)
                        {
                            seenContainers.Add(containerId.Value);
                        }

                        string name = ResolveFriendlyDeviceName(batInfo.Name, containerId, connectedContainers, fallbackDevName);
                        DeviceKind kind = ClassifyDevice(name, batInfo.Id, string.Empty, containerId, connectedContainers);
                        results.Add(new DeviceBatteryInfo(name, (byte)pct, "HID", containerId, kind));
                    }
                }
                catch
                {
                    // Skip inaccessible battery device
                }
            }
        }
        catch
        {
            // Ignore Battery API failure
        }

        // 4. Direct HID Feature/Input Report Battery Strength (UsagePage 0x06 Usage 0x20 / UsagePage 0x85)
        try
        {
            await QueryHidBatteryDevicesAsync(results, seenContainers, connectedContainers, fallbackDevName);
        }
        catch
        {
            // Ignore HID query errors
        }

        return results
            .OrderBy(d => d.Kind)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static async Task<byte?> TryReadBleBatteryLevelAsync(BluetoothLEDevice bleDevice)
    {
        Guid batteryServiceUuid = GattDeviceService.ConvertShortIdToUuid(BatteryServiceShortId);
        GattDeviceServicesResult servicesResult = await bleDevice.GetGattServicesForUuidAsync(
            batteryServiceUuid,
            BluetoothCacheMode.Uncached);

        if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
        {
            servicesResult = await bleDevice.GetGattServicesForUuidAsync(
                GattServiceUuids.Battery,
                BluetoothCacheMode.Cached);
        }

        if (servicesResult.Status == GattCommunicationStatus.Success && servicesResult.Services.Count > 0)
        {
            byte? level = await ReadBatteryCharacteristicFromServicesAsync(servicesResult.Services);
            if (level.HasValue)
            {
                return level.Value;
            }
        }

        GattDeviceServicesResult allServices = await bleDevice.GetGattServicesAsync(BluetoothCacheMode.Cached);
        if (allServices.Status == GattCommunicationStatus.Success && allServices.Services.Count > 0)
        {
            return await ReadBatteryCharacteristicFromServicesAsync(allServices.Services);
        }

        return null;
    }

    private static async Task<byte?> ReadBatteryCharacteristicFromServicesAsync(IReadOnlyList<GattDeviceService> services)
    {
        byte? batteryLevel = null;
        foreach (GattDeviceService service in services)
        {
            using (service)
            {
                if (batteryLevel.HasValue)
                {
                    continue;
                }

                try
                {
                    GattCharacteristicsResult charsResult = await service.GetCharacteristicsForUuidAsync(
                        GattCharacteristicUuids.BatteryLevel,
                        BluetoothCacheMode.Uncached);

                    if (charsResult.Status != GattCommunicationStatus.Success || charsResult.Characteristics.Count == 0)
                    {
                        charsResult = await service.GetCharacteristicsForUuidAsync(
                            GattCharacteristicUuids.BatteryLevel,
                            BluetoothCacheMode.Cached);
                    }

                    if (charsResult.Status == GattCommunicationStatus.Success)
                    {
                        foreach (GattCharacteristic characteristic in charsResult.Characteristics)
                        {
                            GattReadResult readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                            if (readResult.Status != GattCommunicationStatus.Success)
                            {
                                readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Cached);
                            }

                            if (readResult.Status == GattCommunicationStatus.Success &&
                                readResult.Value != null &&
                                readResult.Value.Length > 0)
                            {
                                using var reader = DataReader.FromBuffer(readResult.Value);
                                byte val = reader.ReadByte();
                                if (val <= 100)
                                {
                                    batteryLevel = val;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore restricted service
                }
            }
        }

        return batteryLevel;
    }

    private static async Task QueryHidBatteryDevicesAsync(
        List<DeviceBatteryInfo> results,
        HashSet<Guid> seenContainers,
        IReadOnlyDictionary<Guid, ContainerMetadata> connectedContainers,
        string fallbackDevName)
    {
        HidNative.HidD_GetHidGuid(out Guid hidGuid);
        string selector = $"System.Devices.InterfaceClassGuid:=\"{hidGuid:B}\" AND System.Devices.InterfaceEnabled:=System.StructuredQueryType.Boolean#True";
        DeviceInformationCollection interfaces = await DeviceInformation.FindAllAsync(
            selector,
            new[] { PropContainerId, "System.ItemNameDisplay" });

        foreach (DeviceInformation iface in interfaces)
        {
            try
            {
                Guid? containerId = TryGetContainerId(iface.Properties);
                if (containerId.HasValue && (containerId.Value == SystemRootContainerId || seenContainers.Contains(containerId.Value)))
                {
                    continue;
                }

                if (HidNative.TryReadHidBatteryPercent(iface.Id, out byte pct, out string? productName))
                {
                    if (containerId.HasValue)
                    {
                        seenContainers.Add(containerId.Value);
                    }

                    string rawName = !string.IsNullOrWhiteSpace(productName) ? productName : iface.Name;
                    string name = ResolveFriendlyDeviceName(rawName, containerId, connectedContainers, fallbackDevName);
                    DeviceKind kind = ClassifyDevice(name, iface.Id, string.Empty, containerId, connectedContainers);
                    results.Add(new DeviceBatteryInfo(name, pct, "2.4GHz/HID", containerId, kind));
                }
            }
            catch
            {
                // Skip inaccessible HID interface
            }
        }
    }

    private static DeviceKind ClassifyDevice(
        string name,
        string deviceId,
        string iconHint,
        Guid? containerId,
        IReadOnlyDictionary<Guid, ContainerMetadata> connectedContainers)
    {
        string combinedIcon = iconHint;
        string category = string.Empty;

        if (containerId.HasValue && connectedContainers.TryGetValue(containerId.Value, out ContainerMetadata? meta))
        {
            combinedIcon = $"{iconHint} {meta.IconPath}";
            category = meta.Category;
        }

        if (combinedIcon.Contains("-2033", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-2013", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-2014", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-2015", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-2032", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-3012", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-3014", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-3021", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceKind.Headphones;
        }

        if (combinedIcon.Contains("-2212", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-2002", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-3074", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceKind.Mouse;
        }

        if (combinedIcon.Contains("-2001", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-2210", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-3001", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-3073", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceKind.Keyboard;
        }

        if (combinedIcon.Contains("-2207", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-2005", StringComparison.OrdinalIgnoreCase) ||
            combinedIcon.Contains("-3072", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceKind.Gamepad;
        }

        string text = $"{name} {category} {deviceId}";

        if (ContainsAny(text, "WH-", "XM4", "XM5", "XM6", "AirPods", "Buds", "Headphone", "Headset", "Earphone", "Hands-Free", "Audio", "JBL", "Наушники", "Гарнитура"))
        {
            return DeviceKind.Headphones;
        }

        if (ContainsAny(text, "Mouse", "Мышь", "NIX", "Viper", "DeathAdder", "Basilisk", "Superlight", "MX Master", "MX Anywhere", "Glorious", "Lamzu", "Pulsar", "Attack Shark", "VXE", "Darmoshark"))
        {
            return DeviceKind.Mouse;
        }

        if (ContainsAny(text, "Air75", "NuPhy", "Keychron", "Keyboard", "Клавиатура", "Comfort", "Anne Pro", "Akko", "MonsGeek", "Wooting"))
        {
            return DeviceKind.Keyboard;
        }

        if (ContainsAny(text, "Xbox", "Controller", "Gamepad", "DualSense", "DualShock", "Joy-Con", "8BitDo", "Геймпад", "Контроллер"))
        {
            return DeviceKind.Gamepad;
        }

        return DeviceKind.Battery;
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (string kw in keywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string? GetStringProperty(IReadOnlyDictionary<string, object?> properties, string key)
    {
        if (properties.TryGetValue(key, out object? val) && val != null)
        {
            return val.ToString();
        }
        return null;
    }

    private static async Task<bool> IsPropertySupportedAsync(string propertyName)
    {
        try
        {
            await DeviceInformation.FindAllAsync(
                "System.Devices.Present:=System.StructuredQueryType.Boolean#True",
                new[] { propertyName },
                DeviceInformationKind.Device);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDeviceActivelyConnected(
        IReadOnlyDictionary<string, object?> properties,
        Guid? containerId,
        IReadOnlyDictionary<Guid, ContainerMetadata> connectedContainers)
    {
        if (properties.TryGetValue(PropBleConnectedState, out object? rawBleConn) && rawBleConn is byte bleConnByte)
        {
            return bleConnByte != 0;
        }

        if (containerId.HasValue && connectedContainers.ContainsKey(containerId.Value))
        {
            return true;
        }

        if (properties.TryGetValue(PropConnected, out object? rawConnected) && rawConnected is bool isConnected)
        {
            return isConnected;
        }

        if (connectedContainers.Count > 0 && containerId.HasValue && containerId.Value != SystemRootContainerId)
        {
            return false;
        }

        return true;
    }

    private static string ResolveFriendlyDeviceName(
        string? rawNodeName,
        Guid? containerId,
        IReadOnlyDictionary<Guid, ContainerMetadata> connectedContainers,
        string fallbackName)
    {
        string nodeName = (rawNodeName ?? string.Empty).Trim();

        string[] suffixesToRemove = { " Hands-Free AG", " Hands-Free", " Avrcp Transport", " AVRCP Transport" };
        foreach (string suffix in suffixesToRemove)
        {
            if (nodeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && nodeName.Length > suffix.Length)
            {
                nodeName = nodeName[..^suffix.Length].Trim();
            }
        }

        if (containerId.HasValue &&
            connectedContainers.TryGetValue(containerId.Value, out ContainerMetadata? meta) &&
            !string.IsNullOrWhiteSpace(meta.Name) &&
            !IsGenericDeviceName(meta.Name))
        {
            return meta.Name;
        }

        if (!string.IsNullOrWhiteSpace(nodeName))
        {
            return nodeName;
        }

        if (containerId.HasValue &&
            connectedContainers.TryGetValue(containerId.Value, out ContainerMetadata? fallbackMeta) &&
            !string.IsNullOrWhiteSpace(fallbackMeta.Name))
        {
            return fallbackMeta.Name;
        }

        return fallbackName;
    }

    private static bool IsGenericDeviceName(string name)
    {
        return name.Equals("USBHID", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Wireless Device", StringComparison.OrdinalIgnoreCase)
            || name.Equals("USB Input Device", StringComparison.OrdinalIgnoreCase)
            || name.Equals("USB-устройство ввода", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineSourceLabel(string deviceId)
    {
        if (deviceId.Contains("BTHLE", StringComparison.OrdinalIgnoreCase))
        {
            return "BLE";
        }

        if (deviceId.Contains("BTHENUM", StringComparison.OrdinalIgnoreCase) ||
            deviceId.Contains("BTH", StringComparison.OrdinalIgnoreCase))
        {
            return "Bluetooth";
        }

        return "HID/2.4GHz";
    }

    private static Guid? TryGetContainerId(IReadOnlyDictionary<string, object?> properties)
    {
        string[] keys = { PropContainerId, PropAepContainerId };
        foreach (string key in keys)
        {
            if (properties.TryGetValue(key, out object? rawContainer) && rawContainer != null)
            {
                if (rawContainer is Guid guid && guid != Guid.Empty)
                {
                    return guid;
                }

                if (Guid.TryParse(rawContainer.ToString(), out Guid parsed) && parsed != Guid.Empty)
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static bool TryExtractDeviceBatteryPercent(IReadOnlyDictionary<string, object?> properties, out byte percent)
    {
        string[] candidateKeys =
        {
            PropBatteryLifePercent,
            PropBluetoothBattery,
            PropBatteryPlusCharging,
            PropBatteryPlusChargingText
        };

        foreach (string key in candidateKeys)
        {
            if (properties.TryGetValue(key, out object? rawValue) && rawValue != null)
            {
                if (TryParseBatteryPercent(rawValue, out percent))
                {
                    return true;
                }
            }
        }

        percent = 0;
        return false;
    }

    private static bool TryParseBatteryPercent(object rawValue, out byte percent)
    {
        percent = 0;
        try
        {
            if (rawValue is string strVal)
            {
                string digits = new string(strVal.TakeWhile(c => char.IsDigit(c)).ToArray());
                if (byte.TryParse(digits, out byte parsedStr) && parsedStr <= 100)
                {
                    percent = parsedStr;
                    return true;
                }
                return false;
            }

            int value = Convert.ToInt32(rawValue);
            if (value is >= 0 and <= 100)
            {
                percent = (byte)value;
                return true;
            }
        }
        catch
        {
            // Not convertible
        }

        return false;
    }

    private void UpdateTrayUI(IReadOnlyList<DeviceBatteryInfo> devices)
    {
        BuildContextMenu(devices, isInitial: false);
        SyncTrayIcons(devices, isInitial: false);
    }

    private void SyncTrayIcons(IReadOnlyList<DeviceBatteryInfo> devices, bool isInitial)
    {
        var specs = new List<(Icon Icon, string Tooltip)>();
        Strings s = Localization.Get;

        if (devices.Count == 0)
        {
            string tip = isInitial ? s.Loading : s.NoDevicesTooltip;
            specs.Add((CreateDeviceGlyphIcon(DeviceKind.Battery, shiftRight: false), tip));
        }
        else
        {
            string allSummary = string.Join(Environment.NewLine, devices.Select(d => $"{d.Name}: {d.BatteryPercent}%"));

            foreach (DeviceBatteryInfo dev in devices)
            {
                string devTooltip = $"{dev.Name}: {dev.BatteryPercent}% ({dev.Source})";
                if (devices.Count > 1)
                {
                    devTooltip = $"{dev.Name}: {dev.BatteryPercent}% ({dev.Source}){Environment.NewLine}{allSummary}";
                }

                if (_separateGlyphAndPercent)
                {
                    specs.Add((CreateDeviceGlyphIcon(dev.Kind, shiftRight: true), devTooltip));
                    specs.Add((CreatePercentageTextIcon(dev.BatteryPercent), devTooltip));
                }
                else
                {
                    specs.Add((CreateCompactCombinedIcon(dev.Kind, dev.BatteryPercent), devTooltip));
                }
            }
        }

        while (_trayIcons.Count > specs.Count)
        {
            int lastIdx = _trayIcons.Count - 1;
            NotifyIcon toRemove = _trayIcons[lastIdx];
            _trayIcons.RemoveAt(lastIdx);
            toRemove.MouseUp -= OnNotifyIconMouseUp;
            toRemove.Visible = false;
            Icon? oldIcon = toRemove.Icon;
            toRemove.Dispose();
            oldIcon?.Dispose();
        }

        while (_trayIcons.Count < specs.Count)
        {
            var ni = new NotifyIcon
            {
                ContextMenuStrip = _contextMenu,
                Visible = false
            };
            ni.MouseUp += OnNotifyIconMouseUp;
            _trayIcons.Add(ni);
        }

        for (int i = 0; i < specs.Count; i++)
        {
            NotifyIcon ni = _trayIcons[i];
            Icon? previousIcon = ni.Icon;
            ni.Icon = specs[i].Icon;
            SetTooltipSafe(ni, specs[i].Tooltip);
            if (!ni.Visible)
            {
                ni.Visible = true;
            }
            previousIcon?.Dispose();
        }
    }

    private static void SetTooltipSafe(NotifyIcon notifyIcon, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            text = "Wireless Battery Monitor";
        }

        if (text.Length > 120)
        {
            text = text[..117] + "...";
        }

        notifyIcon.Text = text;
    }

    private void BuildContextMenu(IReadOnlyList<DeviceBatteryInfo> devices, bool isInitial)
    {
        _contextMenu.Items.Clear();
        Strings s = Localization.Get;

        if (isInitial)
        {
            var loadingItem = new ToolStripMenuItem(s.PollingDevices) { Enabled = false };
            _contextMenu.Items.Add(loadingItem);
        }
        else if (devices.Count == 0)
        {
            var emptyItem = new ToolStripMenuItem(s.NoDevicesMenu) { Enabled = false };
            _contextMenu.Items.Add(emptyItem);
        }
        else
        {
            foreach (DeviceBatteryInfo device in devices)
            {
                var item = new ToolStripMenuItem($"{device.Name}: {device.BatteryPercent}% ({device.Source})")
                {
                    Enabled = false
                };
                _contextMenu.Items.Add(item);
            }
        }

        _contextMenu.Items.Add(new ToolStripSeparator());

        var modeItem = new ToolStripMenuItem(s.SeparateIconsMode)
        {
            Checked = _separateGlyphAndPercent
        };
        modeItem.Click += (_, _) =>
        {
            _separateGlyphAndPercent = !_separateGlyphAndPercent;
            UpdateTrayUI(_lastDevices);
        };
        _contextMenu.Items.Add(modeItem);

        var langMenu = new ToolStripMenuItem(s.LanguageMenu);
        foreach (Strings lang in Localization.AllLanguages)
        {
            var langItem = new ToolStripMenuItem(lang.DisplayName)
            {
                Checked = lang.Code.Equals(Localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase)
            };
            string capturedCode = lang.Code;
            langItem.Click += (_, _) =>
            {
                Localization.CurrentLanguage = capturedCode;
                Localization.SaveLanguage(capturedCode);
                UpdateTrayUI(_lastDevices);
            };
            langMenu.DropDownItems.Add(langItem);
        }
        _contextMenu.Items.Add(langMenu);

        var refreshItem = new ToolStripMenuItem(s.RefreshNow);
        refreshItem.Click += async (_, _) => await RefreshDevicesAsync();
        _contextMenu.Items.Add(refreshItem);

        var exitItem = new ToolStripMenuItem(s.Exit);
        exitItem.Click += (_, _) => ExitThread();
        _contextMenu.Items.Add(exitItem);
    }

    protected override void ExitThreadCore()
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();

        foreach (NotifyIcon ni in _trayIcons)
        {
            ni.MouseUp -= OnNotifyIconMouseUp;
            ni.Visible = false;
            Icon? icon = ni.Icon;
            ni.Dispose();
            icon?.Dispose();
        }
        _trayIcons.Clear();

        _contextMenu.Dispose();
        base.ExitThreadCore();
    }

    private static Icon CreateDeviceGlyphIcon(DeviceKind kind, bool shiftRight)
    {
        using var bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            if (shiftRight)
            {
                g.TranslateTransform(2f, 0f);
            }

            DrawGlyph(g, kind, new Pen(Color.White, 2.2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            });
        }

        return BitmapToIcon(bmp);
    }

    private static void DrawGlyph(Graphics g, DeviceKind kind, Pen pen)
    {
        using (pen)
        {
            switch (kind)
            {
                case DeviceKind.Headphones:
                {
                    g.DrawArc(pen, 6, 5, 20, 20, 180, 180);
                    g.DrawLine(pen, 6, 15, 6, 23);
                    g.DrawLine(pen, 26, 15, 26, 23);
                    using GraphicsPath leftCup = RoundedRect(new RectangleF(6, 16, 6f, 9.5f), 2f);
                    g.DrawPath(pen, leftCup);
                    using GraphicsPath rightCup = RoundedRect(new RectangleF(20, 16, 6f, 9.5f), 2f);
                    g.DrawPath(pen, rightCup);
                    break;
                }
                case DeviceKind.Mouse:
                {
                    using GraphicsPath body = RoundedRect(new RectangleF(8, 4, 16, 24), 7f);
                    g.DrawPath(pen, body);
                    g.DrawLine(pen, 16, 4, 16, 13);
                    break;
                }
                case DeviceKind.Keyboard:
                {
                    using GraphicsPath body = RoundedRect(new RectangleF(4, 7, 24, 18), 3f);
                    g.DrawPath(pen, body);
                    for (int i = 0; i < 4; i++)
                    {
                        g.FillRectangle(Brushes.White, 7.5f + i * 4.8f, 11f, 2.5f, 2.5f);
                        g.FillRectangle(Brushes.White, 7.5f + i * 4.8f, 15f, 2.5f, 2.5f);
                    }
                    g.DrawLine(pen, 10, 21, 22, 21);
                    break;
                }
                case DeviceKind.Gamepad:
                {
                    using GraphicsPath body = RoundedRect(new RectangleF(4, 8, 24, 16), 5f);
                    g.DrawPath(pen, body);
                    g.DrawLine(pen, 10, 13, 10, 19);
                    g.DrawLine(pen, 7, 16, 13, 16);
                    g.FillEllipse(Brushes.White, 20, 12, 3, 3);
                    g.FillEllipse(Brushes.White, 23, 15, 3, 3);
                    break;
                }
                default:
                {
                    using GraphicsPath body = RoundedRect(new RectangleF(4, 9, 21, 14), 2.5f);
                    g.DrawPath(pen, body);
                    g.FillRectangle(Brushes.White, 26, 13, 2.5f, 6);
                    g.FillRectangle(Brushes.White, 7, 12, 15, 8);
                    break;
                }
            }
        }
    }

    private static Icon CreatePercentageTextIcon(byte percent)
    {
        using var bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            string text = percent >= 100 ? "100" : $"{percent}%";
            float fontSize = text.Length >= 3 ? 13.5f : 15f;
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };

            g.DrawString(text, font, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
        }

        return BitmapToIcon(bmp);
    }

    private static Icon CreateCompactCombinedIcon(DeviceKind kind, byte percent)
    {
        using var bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            GraphicsState state = g.Save();
            g.TranslateTransform(5f, -1f);
            g.ScaleTransform(0.68f, 0.68f);
            DrawGlyph(g, kind, new Pen(Color.White, 2.4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            });
            g.Restore(state);

            string text = percent >= 100 ? "100" : $"{percent}%";
            using var font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Far,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(text, font, Brushes.White, new RectangleF(0, 16, 32, 16), sf);
        }

        return BitmapToIcon(bmp);
    }

    private static Icon BitmapToIcon(Bitmap bmp)
    {
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal static class HidNative
{
    private const int HidpStatusSuccess = 0x00110000;
    private const uint FileShareReadWrite = 0x00000001 | 0x00000002;
    private const uint OpenExisting = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_VALUE_CAPS
    {
        public ushort UsagePage;
        public byte ReportID;
        [MarshalAs(UnmanagedType.U1)]
        public bool IsAlias;
        public ushort BitField;
        public ushort LinkCollection;
        public ushort LinkUsage;
        public ushort LinkUsagePage;
        [MarshalAs(UnmanagedType.U1)]
        public bool IsRange;
        [MarshalAs(UnmanagedType.U1)]
        public bool IsStringRange;
        [MarshalAs(UnmanagedType.U1)]
        public bool IsDesignatorRange;
        [MarshalAs(UnmanagedType.U1)]
        public bool IsAbsolute;
        [MarshalAs(UnmanagedType.U1)]
        public bool HasNull;
        public byte Reserved;
        public ushort BitSize;
        public ushort ReportCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public ushort[] Reserved2;
        public uint UnitsExp;
        public uint Units;
        public int LogicalMin;
        public int LogicalMax;
        public int PhysicalMin;
        public int PhysicalMax;
        public ushort UsageMin;
        public ushort UsageMax;
        public ushort StringMin;
        public ushort StringMax;
        public ushort DesignatorMin;
        public ushort DesignatorMax;
        public ushort DataIndexMin;
        public ushort DataIndexMax;
    }

    [DllImport("hid.dll", SetLastError = true)]
    public static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern int HidP_GetValueCaps(int reportType, [Out] HIDP_VALUE_CAPS[] valueCaps, ref ushort valueCapsLength, IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection, ushort usage, out uint usageValue, IntPtr preparsedData, byte[] report, uint reportLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, uint reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetInputReport(SafeFileHandle hidDeviceObject, byte[] reportBuffer, uint reportBufferLength);

    [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool HidD_GetProductString(SafeFileHandle hidDeviceObject, byte[] buffer, uint bufferLength);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    public static bool TryReadHidBatteryPercent(string devicePath, out byte percent, out string? productName)
    {
        percent = 0;
        productName = null;

        using SafeFileHandle handle = CreateFile(devicePath, 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return false;
        }

        if (!HidD_GetPreparsedData(handle, out IntPtr ppd))
        {
            return false;
        }

        try
        {
            if (HidP_GetCaps(ppd, out HIDP_CAPS caps) != HidpStatusSuccess)
            {
                return false;
            }

            if (TryReadReportBattery(handle, ppd, reportType: 2, caps.NumberFeatureValueCaps, caps.FeatureReportByteLength, out percent) ||
                TryReadReportBattery(handle, ppd, reportType: 0, caps.NumberInputValueCaps, caps.InputReportByteLength, out percent))
            {
                byte[] prodBuf = new byte[256];
                if (HidD_GetProductString(handle, prodBuf, (uint)prodBuf.Length))
                {
                    productName = System.Text.Encoding.Unicode.GetString(prodBuf).TrimEnd('\0');
                }
                return true;
            }
        }
        finally
        {
            HidD_FreePreparsedData(ppd);
        }

        return false;
    }

    private static bool TryReadReportBattery(
        SafeFileHandle handle,
        IntPtr ppd,
        int reportType,
        ushort numValueCaps,
        ushort reportByteLength,
        out byte percent)
    {
        percent = 0;
        if (numValueCaps == 0 || reportByteLength == 0)
        {
            return false;
        }

        ushort len = numValueCaps;
        var valueCaps = new HIDP_VALUE_CAPS[len];
        if (HidP_GetValueCaps(reportType, valueCaps, ref len, ppd) != HidpStatusSuccess)
        {
            return false;
        }

        foreach (HIDP_VALUE_CAPS vc in valueCaps.Take(len))
        {
            ushort usage = vc.UsageMin;
            bool isBatteryUsage =
                (vc.UsagePage == 0x0006 && usage == 0x0020) ||
                (vc.UsagePage == 0x0085 && (usage == 0x0065 || usage == 0x0066)) ||
                (vc.UsagePage == 0x0084 && usage == 0x0065);

            if (!isBatteryUsage)
            {
                continue;
            }

            byte[] report = new byte[reportByteLength];
            report[0] = vc.ReportID;
            bool ok = reportType == 2
                ? HidD_GetFeature(handle, report, reportByteLength)
                : HidD_GetInputReport(handle, report, reportByteLength);

            if (!ok)
            {
                continue;
            }

            if (HidP_GetUsageValue(reportType, vc.UsagePage, vc.LinkCollection, usage, out uint val, ppd, report, reportByteLength) == HidpStatusSuccess)
            {
                if (vc.LogicalMax > vc.LogicalMin && vc.LogicalMax > 0 && val <= (uint)vc.LogicalMax)
                {
                    int scaled = (int)Math.Round((val - vc.LogicalMin) * 100.0 / (vc.LogicalMax - vc.LogicalMin));
                    if (scaled is > 0 and <= 100)
                    {
                        percent = (byte)scaled;
                        return true;
                    }
                }
                else if (val is > 0 and <= 100)
                {
                    percent = (byte)val;
                    return true;
                }
            }
        }

        return false;
    }
}
