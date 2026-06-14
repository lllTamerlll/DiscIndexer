using System;
using System.Diagnostics;
using System.IO;

namespace Cost_Calculation.Services
{
    /// <summary>
    /// Файловый журнал в %APPDATA%\DiscIndexer\log.txt. В отличие от
    /// Debug.WriteLine (помечен [Conditional("DEBUG")] и вырезается из Release),
    /// пишет в обеих конфигурациях, поэтому сбои в проде остаются
    /// диагностируемыми. Все методы потокобезопасны и никогда не бросают
    /// исключений — журналирование не должно ронять приложение.
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiscIndexer");

        public static readonly string LogFile = Path.Combine(LogDir, "log.txt");

        // По достижении лимита текущий файл уезжает в log.txt.old, чтобы журнал
        // не рос бесконечно. Храним одно поколение — этого хватает для разбора.
        private const long MaxBytes = 1 * 1024 * 1024;

        private static readonly object _gate = new();

        public static void Info(string message) => Write("INFO", message, null);

        public static void Error(string message) => Write("ERROR", message, null);

        public static void Error(string context, Exception ex) => Write("ERROR", context, ex);

        private static void Write(string level, string message, Exception? ex)
        {
            // Вывод в отладчик оставляем для разработки, файл — для прода.
            Debug.WriteLine($"[{level}] {message}{(ex != null ? $": {ex}" : "")}");

            try
            {
                lock (_gate)
                {
                    Directory.CreateDirectory(LogDir);
                    RotateIfNeeded();

                    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                    if (ex != null) line += Environment.NewLine + ex;
                    File.AppendAllText(LogFile, line + Environment.NewLine);
                }
            }
            catch
            {
                // Журналирование принципиально «тихое»: его сбой не должен
                // влиять на работу приложения (и тем более вызывать рекурсию).
            }
        }

        private static void RotateIfNeeded()
        {
            try
            {
                var info = new FileInfo(LogFile);
                if (info.Exists && info.Length > MaxBytes)
                    File.Move(LogFile, LogFile + ".old", overwrite: true);
            }
            catch
            {
                // Не удалось провернуть ротацию — продолжаем писать в текущий файл.
            }
        }
    }
}
