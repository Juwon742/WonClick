using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WonClick.AutoParameter.Models;

namespace WonClick.AutoParameter.Services
{
    public static class AutoParameterPresetService
    {
        // =========================================================
        // JSON 옵션
        // =========================================================

        private static readonly JsonSerializerOptions
            JsonOptions =
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };


        // =========================================================
        // Auto Hold 저장 폴더
        //
        // %APPDATA%\WonClick\AutoParameter
        // =========================================================

        private static readonly string
            HoldDirectory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "WonClick",
                    "AutoParameter");


        // =========================================================
        // Auto Hold 파일
        // =========================================================

        private static readonly string
            HoldFilePath =
                Path.Combine(
                    HoldDirectory,
                    "LastSettings.wcap");


        // =========================================================
        // Hold 파일 경로
        //
        // 디버깅 / 향후 Settings 화면 등에서 사용 가능
        // =========================================================

        public static string GetHoldFilePath()
        {
            return HoldFilePath;
        }


        // =========================================================
        // 일반 Export
        // =========================================================

        public static void Save(
            string filePath,
            AutoParameterPreset preset)
        {
            if (string.IsNullOrWhiteSpace(
                    filePath))
            {
                throw new ArgumentException(
                    "파일 경로가 올바르지 않습니다.",
                    nameof(filePath));
            }


            if (preset == null)
            {
                throw new ArgumentNullException(
                    nameof(preset));
            }


            string? directory =
                Path.GetDirectoryName(
                    filePath);


            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }


            string json =
                JsonSerializer.Serialize(
                    preset,
                    JsonOptions);


            File.WriteAllText(
                filePath,
                json);
        }


        // =========================================================
        // 일반 Import
        // =========================================================

        public static AutoParameterPreset Load(
            string filePath)
        {
            if (string.IsNullOrWhiteSpace(
                    filePath))
            {
                throw new ArgumentException(
                    "파일 경로가 올바르지 않습니다.",
                    nameof(filePath));
            }


            if (!File.Exists(
                    filePath))
            {
                throw new FileNotFoundException(
                    "Auto Parameter 설정 파일을 찾을 수 없습니다.",
                    filePath);
            }


            string json =
                File.ReadAllText(
                    filePath);


            AutoParameterPreset? preset =
                JsonSerializer.Deserialize<
                    AutoParameterPreset>(
                        json,
                        JsonOptions);


            if (preset == null)
            {
                throw new InvalidDataException(
                    "Auto Parameter 설정 파일을 읽을 수 없습니다.");
            }


            if (preset.Version != 1)
            {
                throw new InvalidDataException(
                    "지원하지 않는 Auto Parameter 설정 파일 버전입니다."
                    +
                    "\n\n"
                    +
                    "Version : "
                    +
                    preset.Version);
            }


            preset.Rules ??=
                new List<
                    AutoParameterPresetRule>();


            return preset;
        }


        // =========================================================
        // Auto Hold 저장
        //
        // Revit을 종료했다가 다시 실행해도
        // 마지막 Auto Parameter UI 설정을 유지하기 위한 파일
        // =========================================================

        public static void SaveHold(
            AutoParameterPreset preset)
        {
            if (preset == null)
            {
                throw new ArgumentNullException(
                    nameof(preset));
            }


            Directory.CreateDirectory(
                HoldDirectory);


            Save(
                HoldFilePath,
                preset);
        }


        // =========================================================
        // Auto Hold 불러오기
        //
        // Hold 파일이 없거나 손상된 경우
        // Add-in 실행 자체를 방해하지 않고 null 반환
        // =========================================================

        public static AutoParameterPreset? TryLoadHold()
        {
            try
            {
                if (!File.Exists(
                        HoldFilePath))
                {
                    return null;
                }


                return Load(
                    HoldFilePath);
            }
            catch
            {
                // Hold 파일 하나가 손상되었다고 해서
                // WonClick 전체가 실행 실패하면 안 된다.
                return null;
            }
        }


        // =========================================================
        // Hold 파일 삭제
        //
        // 향후 Reset Settings 기능에서 사용 가능
        // =========================================================

        public static bool DeleteHold()
        {
            try
            {
                if (!File.Exists(
                        HoldFilePath))
                {
                    return true;
                }


                File.Delete(
                    HoldFilePath);


                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}