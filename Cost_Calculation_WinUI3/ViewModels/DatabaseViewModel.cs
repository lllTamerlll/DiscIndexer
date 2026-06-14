using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Cost_Calculation.Models;
using Cost_Calculation.Services;
using Cost_Calculation.ViewModels.Messages;

namespace Cost_Calculation.ViewModels
{
    /// <summary>
    /// Логика вкладки «Базы данных»: 4 профиля, переключение активного, загрузка/
    /// удаление данных, переименование. Файловый ввод-вывод (пикеры, буфер обмена,
    /// диалог подтверждения) остаётся в code-behind (нужны hwnd/XamlRoot) и вызывает
    /// методы этой VM для мутаций состояния. Об изменениях активного профиля VM
    /// уведомляет зависящие вкладки через <see cref="ProfileChangedMessage"/>.
    /// </summary>
    public sealed partial class DatabaseViewModel : ObservableObject
    {
        private readonly ISessionService _session;

        public DatabaseViewModel(ISessionService session)
        {
            _session = session;
        }

        public int ProfileCount => SessionService.ProfileCount;
        public int ActiveProfileIndex => _session.Current.ActiveProfileIndex;

        public ProfileState Profile(int index) => _session.Current.Profiles[index];
        public bool IsActive(int index) => _session.Current.ActiveProfileIndex == index;
        public DiscExport? Export(int index) => _session.Current.Profiles[index].Export;

        public void Rename(int index, string name)
        {
            _session.Current.Profiles[index].Name = name;
            _session.RequestSave();
        }

        public void Swap(int index)
        {
            _session.Current.ActiveProfileIndex = index;
            _session.RequestSave();
            Notify(index);
        }

        /// <summary>Применяет импортированный экспорт к профилю; сохраняет метки,
        /// всё ещё указывающие на существующий диск.</summary>
        public void ApplyImported(int index, DiscExport export)
        {
            var profile = _session.Current.Profiles[index];
            profile.Export = export;

            var validIds = export.Discs.Select(d => d.Id).ToHashSet();
            profile.MarkedIds.RemoveAll(id => !validIds.Contains(id));

            profile.LastUpdated = DateTime.Now;
            _session.RequestSave();

            if (index == _session.Current.ActiveProfileIndex)
                Notify(index);
        }

        /// <summary>Очищает данные профиля; при необходимости выбирает новый
        /// активный профиль. Возвращает новый активный индекс.</summary>
        public int Delete(int index)
        {
            var state = _session.Current;
            var profile = state.Profiles[index];
            profile.Export = null;
            profile.MarkedIds = new List<long>();
            profile.LastUpdated = DateTime.MinValue;

            if (state.ActiveProfileIndex == index)
            {
                state.ActiveProfileIndex = Enumerable
                    .Range(0, SessionService.ProfileCount)
                    .Where(i => i != index && state.Profiles[i].HasData)
                    .Select(i => (int?)i)
                    .FirstOrDefault() ?? 0;
            }

            _session.RequestSave();
            Notify(state.ActiveProfileIndex);
            return state.ActiveProfileIndex;
        }

        private static void Notify(int newIndex) =>
            WeakReferenceMessenger.Default.Send(new ProfileChangedMessage(newIndex));
    }
}
