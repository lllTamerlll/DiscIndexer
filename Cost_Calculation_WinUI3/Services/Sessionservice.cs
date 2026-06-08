using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cost_Calculation.Services
{
    public static class SessionService
    {
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiscIndexer");

        private static readonly string SessionFile = Path.Combine(AppDataDir, "session.json");

        private const int ProfileCount = 4;


        public static SessionState Load()
        {
            try
            {
                if (!File.Exists(SessionFile))
                    return CreateDefault();

                var json = File.ReadAllText(SessionFile);
                var state = JsonSerializer.Deserialize<SessionState>(json,
                    JsonOptions()) ?? CreateDefault();

                while (state.Profiles.Count < ProfileCount)
                    state.Profiles.Add(new ProfileState
                    {
                        Name = $"База данных {state.Profiles.Count + 1}"
                    });

                if (state.ActiveProfileIndex < 0 ||
                    state.ActiveProfileIndex >= ProfileCount)
                    state.ActiveProfileIndex = 0;

                return state;
            }
            catch
            {
                return CreateDefault();
            }
        }

        public static void Save(SessionState state)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                var json = JsonSerializer.Serialize(state, JsonOptions());
                File.WriteAllText(SessionFile, json);
            }
            catch {  }
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

        private static JsonSerializerOptions JsonOptions() => new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }


    public class SessionState
    {
        public int ActiveProfileIndex { get; set; } = 0;
        public List<ProfileState> Profiles { get; set; } = new();
    }

    public class ProfileState
    {
        public string Name { get; set; } = "";

        public string ExportJson { get; set; } = null;

        public List<int> MarkedIds { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    }
}