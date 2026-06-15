using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace Cost_Calculation.Services
{
    /// <summary>Показ модальных диалогов; абстракция для инъекции в ViewModel.</summary>
    public interface IDialogService
    {
        Task<ContentDialogResult> ShowAsync(ContentDialog dialog);
    }

    /// <summary>
    /// Сериализует показ ContentDialog: WinUI допускает только один открытый
    /// диалог одновременно — иначе второй ShowAsync бросает COMException и роняет
    /// приложение. Очередь через семафор откладывает второй диалог до закрытия
    /// первого вместо краша. Регистрируется синглтоном в DI.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
        {
            await _gate.WaitAsync();
            try
            {
                return await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("DialogService ShowAsync failed", ex);
                return ContentDialogResult.None;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
