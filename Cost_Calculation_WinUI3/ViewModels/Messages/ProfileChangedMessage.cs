using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Cost_Calculation.ViewModels.Messages
{
    /// <summary>
    /// Сменился активный профиль (или в него загрузили/удалили данные). Заменяет
    /// прежнее событие DatabasePage.ProfileSwapped и «dirty»-флаги в MainWindow:
    /// зависящие вкладки перестраиваются при следующем заходе. Значение — индекс
    /// нового активного профиля.
    /// </summary>
    public sealed class ProfileChangedMessage : ValueChangedMessage<int>
    {
        public ProfileChangedMessage(int newIndex) : base(newIndex) { }
    }
}
