using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace Cost_Calculation.Services
{
    /// <summary>
    /// Сериализует показ ContentDialog: WinUI допускает только один открытый
    /// диалог одновременно — иначе второй ShowAsync бросает COMException и роняет
    /// приложение. Очередь через семафор откладывает второй диалог до закрытия
    /// первого вместо краша.
    /// </summary>
    public static class DialogService
    {
        private static readonly SemaphoreSlim _gate = new(1, 1);

        public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
        {
            await _gate.WaitAsync();
            try
            {
                return await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DialogService] ShowAsync failed: {ex.Message}");
                return ContentDialogResult.None;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
