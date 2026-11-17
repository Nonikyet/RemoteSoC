# RemoteSoC — Wireless Battery Monitor

> **[English](#english)** | **[Русский](#русский)**

---

<a name="english"></a>

# RemoteSoC — Wireless Battery Monitor (English)

A lightweight, self-contained Windows 10 / 11 system tray utility (.NET 8) that displays the battery levels of all connected wireless devices (headphones, mice, keyboards, controllers) directly in the system tray as clean minimalist white icons and percentages.

## Features

- **Separate System Tray Elements**: For each connected device, the app renders a minimalist white outline icon matching the device type (headphones, mouse, keyboard, controller, or battery) alongside a separate tray element showing the battery percentage (`81%`, `60%`, `93%`). A compact mode (1 combined tray icon per device) is also available from the tray menu.
- **Broad Wireless Device Support (Official Windows APIs Only)**:
  - **Bluetooth LE (GATT)**: Reads the standard `Battery Service (0x180F)` and `Battery Level (0x2A19)` characteristic via `Windows.Devices.Bluetooth.GenericAttributeProfile`.
  - **Bluetooth Classic (HFP / PnP)**: Reads the system property `DEVPKEY_Bluetooth_Battery` (`{104ea319-6ee2-4701-bd47-8ddbf425bbe5} 2`) and `System.Devices.BatteryPlusCharging` via `Windows.Devices.Enumeration`.
  - **2.4 GHz Wireless / USB Dongles / HID**: Queries `Windows.Devices.Enumeration` (`System.Devices.BatteryLifePercent`), `Windows.Devices.Power.Battery`, and standard HID Battery Strength reports (`hid.dll`, UsagePage `0x06` Usage `0x20`, UsagePage `0x85`).
  - Automatic device deduplication by `System.Devices.ContainerId` and filtering of actively connected device containers.
- **Smart Installer & Uninstaller (`WirelessBatteryInstaller.exe`)**:
  - Prompts for interface language before installation.
  - Asks whether to create a Desktop shortcut (defaults to **No**).
  - Automatically adds the application to the **Start Menu** by default with native support for uninstalling directly from the Start Menu right-click context menu (**Uninstall**).
  - Single-instance protection via a global `Mutex`, automatic polling every 60 seconds, tooltip truncation to 120 characters, and context menu activation on both left and right mouse clicks.

## Supported Languages

Both the installer and the system tray application are fully localized into 6 languages (**EN, DE, RU, BE, PL, UK**):

- **EN** — English
- **DE** — Deutsch
- **RU** — Русский
- **BE** — Беларуская
- **PL** — Polski
- **UK** — Українська

The language is selected during installation and can also be switched on the fly at any time directly from the system tray context menu.

## Installation

1. Download the standalone installer **`WirelessBatteryInstaller.exe`** from [**Releases (v1.0.0)**](https://github.com/Nonikyet/RemoteSoC/releases/tag/v1.0.0).
2. Run `WirelessBatteryInstaller.exe`.
3. Select your preferred interface language (**EN, DE, RU, BE, PL, UK**).
4. Choose whether to create a Desktop shortcut (default is **No**) and select the installation folder.
5. The application will automatically be added to the Start Menu and launched in the system tray.

## Building from Source

Requirements: **.NET 8.0 SDK** (Windows x64). No third-party NuGet packages are required.

```powershell
# 1. Build the self-contained single-file WirelessBatteryMonitor.exe
dotnet publish WirelessBatteryMonitor\WirelessBatteryMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# 2. Build the standalone single-file installer WirelessBatteryInstaller.exe
dotnet publish WirelessBatteryInstaller\WirelessBatteryInstaller.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Credits

**Idea & Concept: 100% Human (Nonikyet)**

**Code & Implementation: 100% Gemini 4 Argon**

## License

This project is licensed under the [MIT License](LICENSE).

---

<a name="русский"></a>

# RemoteSoC — Wireless Battery Monitor (Русский)

Автономная утилита для Windows 10 / 11 (.NET 8), отображающая заряд аккумулятора всех подключенных беспроводных устройств (наушников, мышек, клавиатур, геймпадов) прямо в системном трее в виде минималистичных белых иконок и процентов заряда.

## Возможности

- **Отдельные элементы в системном трее**: для каждого подключенного устройства приложение создаёт минималистичную белую контурную иконку по типу устройства (наушники, мышь, клавиатура, геймпад или батарея) и соседний элемент трея с процентом заряда (`81%`, `60%`, `93%`). Также в меню трея доступен компактный режим (1 общая иконка на устройство).
- **Широкая поддержка беспроводных устройств (только официальные API Windows)**:
  - **Bluetooth LE (GATT)**: чтение стандартного сервиса `Battery Service (0x180F)` и характеристики `Battery Level (0x2A19)` через `Windows.Devices.Bluetooth.GenericAttributeProfile`.
  - **Bluetooth Classic (HFP / PnP)**: чтение системного свойства `DEVPKEY_Bluetooth_Battery` (`{104ea319-6ee2-4701-bd47-8ddbf425bbe5} 2`) и `System.Devices.BatteryPlusCharging` через `Windows.Devices.Enumeration`.
  - **2.4 ГГц радиоканал / USB-донглы / HID**: опрос через `Windows.Devices.Enumeration` (`System.Devices.BatteryLifePercent`), `Windows.Devices.Power.Battery` и стандартные отчёты HID Battery Strength (`hid.dll`, UsagePage `0x06` Usage `0x20`, UsagePage `0x85`).
  - Автоматическая дедупликация устройств по `System.Devices.ContainerId` и фильтрация только активно подключенных контейнеров устройств.
- **Умный установщик и деинсталлятор (`WirelessBatteryInstaller.exe`)**:
  - Запрашивает язык интерфейса перед установкой.
  - Спрашивает, нужно ли создавать ярлык на рабочем столе (по умолчанию — **Нет**).
  - Автоматически добавляет приложение в меню **«Пуск»** по умолчанию с поддержкой штатного удаления прямо из контекстного меню «Пуск» по кнопке **«Удалить»**.
  - Защита от повторного запуска через глобальный `Mutex`, автоматическое обновление каждые 60 секунд, обрезка тултипа до 120 символов и открытие контекстного меню как по левому, так и по правому клику мыши.

## Поддерживаемые языки

Установщик и приложение в системном трее полностью локализованы на 6 языков (**EN, DE, RU, BE, PL, UK**):

- **EN** — English
- **DE** — Deutsch
- **RU** — Русский
- **BE** — Беларуская
- **PL** — Polski
- **UK** — Українська

Язык выбирается при установке, а также может быть переключён на лету в любой момент прямо из контекстного меню в системном трее.

## Установка

1. Скачайте автономный установщик **`WirelessBatteryInstaller.exe`** из раздела [**Releases (v1.0.0)**](https://github.com/Nonikyet/RemoteSoC/releases/tag/v1.0.0).
2. Запустите `WirelessBatteryInstaller.exe`.
3. Выберите предпочитаемый язык интерфейса (**EN, DE, RU, BE, PL, UK**).
4. Укажите, нужно ли создавать ярлык на рабочем столе (по умолчанию — **Нет**), и выберите папку установки.
5. Приложение автоматически добавится в меню «Пуск» и запустится в системном трее.

## Сборка из исходного кода

Требования: **.NET 8.0 SDK** (Windows x64). Сторонние NuGet-пакеты не требуются.

```powershell
# 1. Сборка автономного single-file WirelessBatteryMonitor.exe
dotnet publish WirelessBatteryMonitor\WirelessBatteryMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# 2. Сборка автономного single-file установщика WirelessBatteryInstaller.exe
dotnet publish WirelessBatteryInstaller\WirelessBatteryInstaller.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Авторство

**Idea & Concept: 100% Human (Nonikyet)**

**Code & Implementation: 100% Gemini 4 Argon**

## Лицензия

Проект распространяется под лицензией [MIT License](LICENSE).
