using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Cost_Calculation.Models;

namespace Cost_Calculation.Services
{
    public class ImportResult
    {
        public DiscExport? Export { get; set; }
        public string? Error { get; set; }
        public bool Success => Export != null && Error == null;
    }

    public static class DiscImportService
    {
        private static readonly JsonSerializerOptions ImportOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public static async Task<ImportResult?> ImportFromFileAsync(IntPtr hwnd)
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = PickerLocationId.Downloads;

            var file = await picker.PickSingleFileAsync();
            if (file == null) return null;

            return await ParseFileAsync(file.Path);
        }

        public static string Serialize(DiscExport export) =>
            JsonSerializer.Serialize(export, ImportOptions);

        private static async Task<ImportResult> ParseFileAsync(string filePath)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);

                if (string.IsNullOrWhiteSpace(json))
                    return new ImportResult { Error = "Файл пустой." };

                DiscExport? export;
                try
                {
                    export = JsonSerializer.Deserialize<DiscExport>(json, ImportOptions);
                }
                catch (JsonException ex)
                {
                    return new ImportResult
                    {
                        Error = $"Файл содержит некорректный JSON:\n{ex.Message}"
                    };
                }

                if (export == null)
                    return new ImportResult { Error = "Не удалось прочитать файл." };

                if (export.Discs == null || export.Discs.Count == 0)
                    return new ImportResult
                    {
                        Error = "Файл не содержит дисков.\n" +
                                "Убедитесь что экспортировали правильный файл из Zenless Optimizer."
                    };

                DiscIdentity.AssignStableIds(export.Discs);

                return new ImportResult { Export = export };
            }
            catch (FileNotFoundException)
            {
                return new ImportResult { Error = "Файл не найден." };
            }
            catch (UnauthorizedAccessException)
            {
                return new ImportResult
                {
                    Error = "Нет доступа к файлу.\n" +
                            "Попробуйте запустить программу от имени администратора."
                };
            }
            catch (Exception ex)
            {
                return new ImportResult { Error = $"Неизвестная ошибка:\n{ex.Message}" };
            }
        }
    }
}
