using System;
using System.Collections.Generic;
using System.Linq;
using Cost_Calculation.Services;

namespace Cost_Calculation.ViewModels
{
    /// <summary>
    /// Логика вкладки «Агенты»: ростер аккаунта (ключи агентов активного профиля),
    /// поиск, добавление/удаление и приоритеты дисковых сетов. Карточки и диалоги
    /// строятся императивно в code-behind и обращаются к этой VM за данными и
    /// мутациями.
    /// </summary>
    public sealed class AgentsViewModel
    {
        private readonly ISessionService _session;

        // Ссылка на список ключей агентов активного профиля.
        private List<string> _owned = new();

        public AgentsViewModel(ISessionService session)
        {
            _session = session;
        }

        /// <summary>Перечитывает ростер из активного профиля.</summary>
        public void LoadAccount() => _owned = _session.Current.ActiveProfile.OwnedAgentKeys;

        public int OwnedCount => _owned.Count;

        // Агенты аккаунта в порядке каталога (по имени).
        public List<Agent> OwnedAgents() =>
            AgentCatalog.All.Where(a => _owned.Contains(a.Key)).ToList();

        public List<Agent> AvailableAgents() =>
            AgentCatalog.All.Where(a => !_owned.Contains(a.Key)).ToList();

        /// <summary>Отфильтрованный поиском список агентов аккаунта.</summary>
        public List<Agent> FilterOwned(string? query)
        {
            var owned = OwnedAgents();
            query = (query ?? "").Trim();
            return string.IsNullOrEmpty(query)
                ? owned
                : owned.Where(a =>
                    a.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    a.Key.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public void AddAgents(IEnumerable<string> keys)
        {
            var set = keys.ToHashSet();
            // Добавляем в порядке каталога, без дублей.
            foreach (var a in AgentCatalog.All)
                if (set.Contains(a.Key) && !_owned.Contains(a.Key))
                    _owned.Add(a.Key);
            _session.RequestSave();
        }

        public bool RemoveAgent(string key)
        {
            if (!_owned.Remove(key)) return false;
            _session.RequestSave();
            return true;
        }

        // ── Приоритеты дисковых сетов ────────────────────────────────────
        public AgentPriority CurrentPriority(string agentKey) =>
            _session.Current.ActiveProfile.AgentPriorities
                .FirstOrDefault(a => a.AgentKey == agentKey)
            ?? new AgentPriority { AgentKey = agentKey };

        public void SavePriority(string agentKey, List<string> fourPiece, List<string> twoPieceOnly)
        {
            var profile = _session.Current.ActiveProfile;
            var entry = profile.AgentPriorities.FirstOrDefault(a => a.AgentKey == agentKey);
            if (entry == null)
            {
                entry = new AgentPriority { AgentKey = agentKey };
                profile.AgentPriorities.Add(entry);
            }
            entry.FourPieceSets = fourPiece;
            entry.TwoPieceSets = twoPieceOnly;
            _session.RequestSave();
        }
    }
}
