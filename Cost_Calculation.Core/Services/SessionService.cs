using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Cost_Calculation.Models;

namespace Cost_Calculation.Services
{
    /// <summary>
    /// Единственный владелец состояния сессии. Состояние загружается с диска
    /// один раз (Current), все страницы работают с одним объектом в памяти.
    /// Сохранение — с дебаунсом и атомарной заменой файла.
    /// </summary>
    public static class SessionService
    {
        public const int ProfileCount = 4;

        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiscIndexer");

        private static readonly string SessionFile = Path.Combine(AppDataDir, "session.json");

        private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(800);

        private static SessionState? _current;
        private static CancellationTokenSource? _pendingSave;

        // Сериализует запись на диск между фоновым и синхронным сохранением.
        private static readonly object _ioLock = new();

        public static SessionState Current => _current ??= LoadFromDisk();

        /// <summary>
        /// Планирует сохранение с дебаунсом. Вызывать с UI-потока — продолжение
        /// после задержки выполняется в том же контексте, поэтому сериализация
        /// не конкурирует с мутациями состояния.
        /// </summary>
        public static void RequestSave()
        {
            _pendingSave?.Cancel();
            var cts = _pendingSave = new CancellationTokenSource();
            _ = SaveAfterDelayAsync(cts.Token);
        }

        private static async Task SaveAfterDelayAsync(CancellationToken token)
        {
            try { await Task.Delay(SaveDelay, token); }
            catch (TaskCanceledException) { return; }
            if (token.IsCancellationRequested || _current == null) return;

            // Сериализация — на вызывающем (UI) потоке: она не конкурирует с
            // мутациями состояния. А вот сам ввод-вывод на диск с непредсказуемой
            // задержкой уводим в фон, чтобы не подвешивать интерфейс.
            string json;
            try { json = JsonSerializer.Serialize(_current, JsonOptions); }
            catch (Exception ex)
            {
                Logger.Error("SessionService serialize failed", ex);
                return;
            }

            try { await Task.Run(() => WriteToDisk(json, token)); }
            catch (Exception ex) { Logger.Error("SessionService background save failed", ex); }
        }

        /// <summary>
        /// Синхронное сохранение — для завершения работы, когда фоновая запись
        /// может не успеть до выхода из процесса.
        /// </summary>
        public static void SaveNow()
        {
            if (_current == null) return;
            // Отменяем отложенное фоновое сохранение: оно несёт более старый
            // снимок и не должно перетереть данные, которые пишем здесь.
            _pendingSave?.Cancel();

            try { WriteToDisk(JsonSerializer.Serialize(_current, JsonOptions), CancellationToken.None); }
            catch (Exception ex) { Logger.Error("SessionService synchronous save failed", ex); }
        }

        private static void WriteToDisk(string json, CancellationToken token)
        {
            // Запись на диск сериализуется: фоновое (отложенное) и синхронное
            // (при выходе) сохранения не должны одновременно делать File.Move в
            // один и тот же файл — иначе гонка финальной замены может потерять
            // последнее сохранение.
            lock (_ioLock)
            {
                // Если отложенное сохранение успели отменить (его обогнал
                // SaveNow при закрытии) — не перетираем свежие данные старыми.
                if (token.IsCancellationRequested) return;

                Directory.CreateDirectory(AppDataDir);
                // Атомарная запись: упавший посреди записи процесс не повредит
                // основной файл — недописанным останется только временный. Имя
                // временного файла уникально на всякий случай.
                var tmp = SessionFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, SessionFile, overwrite: true);
            }
        }

        private static SessionState LoadFromDisk()
        {
            SessionState state;
            try
            {
                if (!File.Exists(SessionFile))
                    return CreateDefault();

                var json = File.ReadAllText(SessionFile);
                state = JsonSerializer.Deserialize<SessionState>(json, JsonOptions)
                        ?? CreateDefault();
            }
            catch (Exception ex)
            {
                Logger.Error("SessionService load failed", ex);
                BackupCorruptFile();
                return CreateDefault();
            }

            while (state.Profiles.Count < ProfileCount)
                state.Profiles.Add(new ProfileState
                {
                    Name = $"База данных {state.Profiles.Count + 1}"
                });

            if (state.ActiveProfileIndex < 0 ||
                state.ActiveProfileIndex >= ProfileCount)
                state.ActiveProfileIndex = 0;

            foreach (var profile in state.Profiles)
                MigrateAndNormalize(profile);

            return state;
        }

        private static void MigrateAndNormalize(ProfileState profile)
        {
            // Старый формат хранил экспорт сериализованной строкой внутри JSON.
            if (profile.Export == null && !string.IsNullOrEmpty(profile.ExportJson))
            {
                try
                {
                    profile.Export = JsonSerializer.Deserialize<DiscExport>(
                        profile.ExportJson, JsonOptions);
                }
                catch (Exception ex)
                {
                    Logger.Error("SessionService legacy export migration failed", ex);
                }
                profile.ExportJson = null;
            }

            if (profile.Export == null) return;

            DiscIdentity.AssignStableIds(profile.Export.Discs);

            // Метки, не указывающие ни на один диск (в т.ч. позиционные метки
            // старого формата), отбрасываются.
            var validIds = profile.Export.Discs.Select(d => d.Id).ToHashSet();
            profile.MarkedIds.RemoveAll(id => !validIds.Contains(id));
        }

        private static void BackupCorruptFile()
        {
            try
            {
                if (File.Exists(SessionFile))
                    File.Copy(SessionFile, SessionFile + ".bak", overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Error("SessionService corrupt-file backup failed", ex);
            }
        }

        private static SessionState CreateDefault()
        {
            var state = new SessionState { ActiveProfileIndex = 0 };
            for (int i = 0; i < ProfileCount; i++)
                state.Profiles.Add(new ProfileState
                {
                    Name = $"База данных {i + 1}"
                });
            return state;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
    }


    public class SessionState
    {
        public int ActiveProfileIndex { get; set; }
        public List<ProfileState> Profiles { get; set; } = new();
        public WindowPlacement? Window { get; set; }

        [JsonIgnore]
        public ProfileState ActiveProfile => Profiles[ActiveProfileIndex];
    }

    public class ProfileState
    {
        public string Name { get; set; } = "";

        public DiscExport? Export { get; set; }

        // Поле старого формата; после миграции при загрузке всегда null.
        public string? ExportJson { get; set; }

        public List<long> MarkedIds { get; set; } = new();

        // Аккаунт игрока: ключи агентов (совпадают с именами карточек в Assets/Agents).
        public List<string> OwnedAgentKeys { get; set; } = new();

        // Приоритеты дисковых сетов по агентам — основа для советов по фарму.
        public List<AgentPriority> AgentPriorities { get; set; } = new();

        public DateTime LastUpdated { get; set; } = DateTime.MinValue;

        [JsonIgnore]
        public bool HasData => Export != null && Export.Discs.Count > 0;
    }

    /// <summary>
    /// Выбор приоритетных дисковых сетов для одного агента.
    /// Сет в FourPieceSets фармится на 4 части и автоматически закрывает 2 части,
    /// поэтому в TwoPieceSets хранятся только сеты, нужные исключительно на 2 части.
    /// </summary>
    public class AgentPriority
    {
        public string AgentKey { get; set; } = "";
        public List<string> FourPieceSets { get; set; } = new();
        public List<string> TwoPieceSets { get; set; } = new();
    }

    public class WindowPlacement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsMaximized { get; set; } = true;
    }
}
