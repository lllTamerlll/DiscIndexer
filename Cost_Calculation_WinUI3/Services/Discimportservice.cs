using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using Cost_Calculation.Models;
using Newtonsoft.Json;

namespace Cost_Calculation.Services
{
    public class ImportResult
    {
        public DiscExport Export { get; set; }
        public string Error { get; set; }
        public bool Success => Export != null && Error == null;
    }

    public static class DiscImportService
    {
        private static readonly string SettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiscIndexer", "last_import_folder.txt");

        public static async Task<ImportResult> ImportFromFileAsync(Window window)
        {
            var picker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add("*");

            picker.SuggestedStartLocation = PickerLocationId.Downloads;

            var file = await picker.PickSingleFileAsync();
            if (file == null) return null;

            SaveLastFolder(Path.GetDirectoryName(file.Path));

            return await ParseFileAsync(file.Path);
        }

        private static void SaveLastFolder(string folder)
        {
            try
            {
                if (string.IsNullOrEmpty(folder)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                File.WriteAllText(SettingsFile, folder);
            }
            catch {  }
        }

        private static async Task<ImportResult> ParseFileAsync(string filePath)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);

                if (string.IsNullOrWhiteSpace(json))
                    return new ImportResult { Error = "Файл пустой." };

                DiscExport export;
                try
                {
                    export = JsonConvert.DeserializeObject<DiscExport>(json);
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

                if (export.discs == null || export.discs.Count == 0)
                    return new ImportResult
                    {
                        Error = "Файл не содержит дисков.\n" +
                                "Убедитесь что экспортировали правильный файл из Zenless Optimizer."
                    };

                for (int i = 0; i < export.discs.Count; i++)
                    export.discs[i].Id = i;

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