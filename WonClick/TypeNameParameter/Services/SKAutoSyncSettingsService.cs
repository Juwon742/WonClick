using System;
using System.IO;

namespace WonClick.TypeNameParameter.Services
{
    /// <summary>
    /// SK Auto Sync ON / OFF 상태를 저장한다.
    ///
    /// 기본값:
    /// ON
    ///
    /// 저장 위치:
    /// %APPDATA%\WonClick\TypeNameParameter\SKAutoSync.settings
    ///
    /// 실행 중에는 메모리 Cache를 사용하므로
    /// Updater가 실행될 때마다 디스크를 읽지 않는다.
    /// </summary>
    public static class SKAutoSyncSettingsService
    {
        private const string EnabledValue =
            "ON";


        private const string DisabledValue =
            "OFF";


        private static bool?
            _cachedEnabled;


        // =========================================================
        // Enabled
        // =========================================================

        public static bool IsEnabled
        {
            get
            {
                if (_cachedEnabled.HasValue)
                {
                    return
                        _cachedEnabled.Value;
                }


                _cachedEnabled =
                    LoadEnabled();


                return
                    _cachedEnabled.Value;
            }
        }


        // =========================================================
        // Set
        // =========================================================

        public static void SetEnabled(
            bool enabled)
        {
            _cachedEnabled =
                enabled;


            try
            {
                string filePath =
                    GetSettingsFilePath();


                string? directory =
                    Path.GetDirectoryName(
                        filePath);


                if (!string.IsNullOrWhiteSpace(
                        directory))
                {
                    Directory.CreateDirectory(
                        directory);
                }


                File.WriteAllText(
                    filePath,
                    enabled
                        ? EnabledValue
                        : DisabledValue);
            }
            catch
            {
                // 메모리 상태는 이미 적용되었으므로
                // 파일 저장 실패가 현재 Session 동작을 막지는 않는다.
            }
        }


        // =========================================================
        // Toggle
        // =========================================================

        public static bool Toggle()
        {
            bool newValue =
                !IsEnabled;


            SetEnabled(
                newValue);


            return newValue;
        }


        // =========================================================
        // Load
        // =========================================================

        private static bool LoadEnabled()
        {
            try
            {
                string filePath =
                    GetSettingsFilePath();


                if (!File.Exists(
                        filePath))
                {
                    return true;
                }


                string value =
                    File.ReadAllText(
                        filePath)
                    .Trim();


                return
                    !string.Equals(
                        value,
                        DisabledValue,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }


        // =========================================================
        // Path
        // =========================================================

        private static string GetSettingsFilePath()
        {
            string appData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);


            return
                Path.Combine(
                    appData,
                    "WonClick",
                    "TypeNameParameter",
                    "SKAutoSync.settings");
        }
    }
}
