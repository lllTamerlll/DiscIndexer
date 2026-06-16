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

        /// <summary>Итог синхронизации импорта: совпало / добавлено / удалено и
        /// был ли профиль непустым (тогда это слияние, и итог стоит показать).</summary>
        public readonly record struct ImportSummary(int Matched, int Added, int Removed, bool HadData);

        /// <summary>Синхронизирует профиль с новым экспортом по стабильным ID:
        /// совпавшие диски остаются вместе с метками и замками, пропавшие выбывают,
        /// новые добавляются. Возвращает сводку расхождения.</summary>
        public ImportSummary ApplyImported(int index, DiscExport export)
        {
            var profile = _session.Current.Profiles[index];

            var oldIds = profile.Export?.Discs.Select(d => d.Id).ToHashSet()
                         ?? new HashSet<long>();
            var newIds = export.Discs.Select(d => d.Id).ToHashSet();
            int matched = newIds.Count(oldIds.Contains);
            int added = newIds.Count - matched;
            int removed = oldIds.Count(id => !newIds.Contains(id));
            bool hadData = oldIds.Count > 0;

            profile.Export = export;
            profile.MarkedIds.RemoveAll(id => !newIds.Contains(id));
            profile.LockedIds.RemoveAll(id => !newIds.Contains(id));

            profile.LastUpdated = DateTime.Now;
            _session.RequestSave();

            if (index == _session.Current.ActiveProfileIndex)
                Notify(index);

            return new ImportSummary(matched, added, removed, hadData);
        }

        /// <summary>Полностью стирает профиль (диски, метки, замки, агенты,
        /// приоритеты, имя → стандартное); при необходимости выбирает новый
        /// активный профиль. Возвращает новый активный индекс.</summary>
        public int Delete(int index)
        {
            var state = _session.Current;
            // Полный сброс слота: новый чистый профиль со стандартным именем.
            state.Profiles[index] = new ProfileState { Name = $"База данных {index + 1}" };

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
