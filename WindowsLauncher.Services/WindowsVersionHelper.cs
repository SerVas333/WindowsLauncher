using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace WindowsLauncher.Services
{
    /// <summary>
    /// Помощник для определения версии Windows и выбора подходящего сервиса клавиатуры
    /// </summary>
    public static class WindowsVersionHelper
    {
        #region Windows API

        [StructLayout(LayoutKind.Sequential)]
        public struct OSVERSIONINFOEX
        {
            public uint dwOSVersionInfoSize;
            public uint dwMajorVersion;
            public uint dwMinorVersion;
            public uint dwBuildNumber;
            public uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int RtlGetVersion(out OSVERSIONINFOEX versionInfo);

        #endregion

        private static WindowsVersion? _cachedVersion;
        private static readonly object _lock = new object();

        /// <summary>
        /// Получить информацию о версии Windows
        /// </summary>
        public static WindowsVersion GetWindowsVersion()
        {
            lock (_lock)
            {
                if (_cachedVersion.HasValue)
                    return _cachedVersion.Value;

                _cachedVersion = DetectWindowsVersion();
                return _cachedVersion.Value;
            }
        }

        private static WindowsVersion DetectWindowsVersion()
        {
            try
            {
                // Пытаемся использовать RtlGetVersion для получения реальной версии
                var versionInfo = new OSVERSIONINFOEX
                {
                    dwOSVersionInfoSize = (uint)Marshal.SizeOf<OSVERSIONINFOEX>()
                };

                int result = RtlGetVersion(out versionInfo);
                if (result == 0) // STATUS_SUCCESS
                {
                    return ClassifyWindowsVersion(versionInfo.dwMajorVersion, versionInfo.dwMinorVersion, versionInfo.dwBuildNumber);
                }
            }
            catch (Exception)
            {
                // Fallback к Environment.OSVersion
            }

            // Fallback метод через Environment.OSVersion
            var osVersion = Environment.OSVersion;
            return ClassifyWindowsVersion((uint)osVersion.Version.Major, (uint)osVersion.Version.Minor, (uint)osVersion.Version.Build);
        }

        private static WindowsVersion ClassifyWindowsVersion(uint major, uint minor, uint build)
        {
            // Windows 11: Build 22000 и выше
            if (major == 10 && minor == 0 && build >= 22000)
            {
                return WindowsVersion.Windows11;
            }

            // Windows 10: Major=10, Minor=0, Build < 22000
            if (major == 10 && minor == 0 && build < 22000)
            {
                return WindowsVersion.Windows10;
            }

            // Windows 8.1: Major=6, Minor=3
            if (major == 6 && minor == 3)
            {
                return WindowsVersion.Windows81;
            }

            // Windows 8: Major=6, Minor=2
            if (major == 6 && minor == 2)
            {
                return WindowsVersion.Windows8;
            }

            // Windows 7: Major=6, Minor=1
            if (major == 6 && minor == 1)
            {
                return WindowsVersion.Windows7;
            }

            // Более старые версии или неизвестные
            if (major < 6)
            {
                return WindowsVersion.WindowsLegacy;
            }

            // Новые версии (будущие)
            if (major > 10 || (major == 10 && build > 25000))
            {
                return WindowsVersion.WindowsFuture;
            }

            return WindowsVersion.WindowsUnknown;
        }

        /// <summary>
        /// Проверить, поддерживает ли текущая версия Windows TabTip.exe
        /// </summary>
        public static bool SupportsTabTip()
        {
            var version = GetWindowsVersion();
            return version == WindowsVersion.Windows10 || 
                   version == WindowsVersion.Windows11 ||
                   version == WindowsVersion.Windows8 ||
                   version == WindowsVersion.Windows81;
        }

        /// <summary>
        /// Проверить, использует ли текущая версия Windows новый TextInputHost
        /// </summary>
        public static bool UsesTextInputHost()
        {
            var version = GetWindowsVersion();
            return version == WindowsVersion.Windows11 || version == WindowsVersion.WindowsFuture;
        }

        /// <summary>
        /// Проверить, нужно ли использовать COM интерфейс ITipInvocation
        /// </summary>
        public static bool RequiresITipInvocation()
        {
            var version = GetWindowsVersion();
            return version == WindowsVersion.Windows10;
        }

        /// <summary>
        /// Получить описание версии Windows для логирования
        /// </summary>
        public static string GetVersionDescription()
        {
            var version = GetWindowsVersion();
            var osVersion = Environment.OSVersion;
            
            return $"{version} (Build {osVersion.Version.Build})";
        }

        /// <summary>
        /// Проверить совместимость с сенсорной клавиатурой
        /// </summary>
        public static TouchKeyboardCompatibility GetTouchKeyboardCompatibility()
        {
            var version = GetWindowsVersion();

            return version switch
            {
                WindowsVersion.Windows11 => TouchKeyboardCompatibility.TextInputHost,
                WindowsVersion.Windows10 => TouchKeyboardCompatibility.TabTipWithCOM,
                WindowsVersion.Windows81 => TouchKeyboardCompatibility.TabTipLegacy,
                WindowsVersion.Windows8 => TouchKeyboardCompatibility.TabTipLegacy,
                WindowsVersion.Windows7 => TouchKeyboardCompatibility.OSKOnly,
                WindowsVersion.WindowsLegacy => TouchKeyboardCompatibility.OSKOnly,
                WindowsVersion.WindowsFuture => TouchKeyboardCompatibility.TextInputHost,
                _ => TouchKeyboardCompatibility.Unknown
            };
        }

        /// <summary>
        /// Получить рекомендуемый путь к исполняемому файлу клавиатуры
        /// </summary>
        public static string GetKeyboardExecutablePath()
        {
            var compatibility = GetTouchKeyboardCompatibility();

            return compatibility switch
            {
                TouchKeyboardCompatibility.TextInputHost => @"C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe",
                TouchKeyboardCompatibility.TabTipWithCOM => @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe",
                TouchKeyboardCompatibility.TabTipLegacy => @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe",
                TouchKeyboardCompatibility.OSKOnly => @"C:\Windows\System32\osk.exe",
                _ => @"C:\Windows\System32\osk.exe"
            };
        }
    }

    /// <summary>
    /// Версии Windows
    /// </summary>
    public enum WindowsVersion
    {
        WindowsUnknown,
        WindowsLegacy,    // Windows XP, Vista и ранее
        Windows7,         // Windows 7
        Windows8,         // Windows 8
        Windows81,        // Windows 8.1
        Windows10,        // Windows 10
        Windows11,        // Windows 11
        WindowsFuture     // Будущие версии
    }

    /// <summary>
    /// Совместимость с сенсорной клавиатурой
    /// </summary>
    public enum TouchKeyboardCompatibility
    {
        Unknown,              // Неизвестная совместимость
        OSKOnly,              // Только классическая экранная клавиатура (osk.exe)
        TabTipLegacy,         // TabTip.exe без COM интерфейса
        TabTipWithCOM,        // TabTip.exe с COM интерфейсом ITipInvocation (Windows 10)
        TextInputHost         // TextInputHost.exe с WinRT API (Windows 11+)
    }
}