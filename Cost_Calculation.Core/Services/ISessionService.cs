namespace Cost_Calculation.Services
{
    /// <summary>
    /// Владелец состояния сессии. Состояние загружается с диска один раз,
    /// все ViewModel работают с одним объектом в памяти. Реализация —
    /// <see cref="SessionService"/> (регистрируется синглтоном в DI).
    /// </summary>
    public interface ISessionService
    {
        /// <summary>Единственный экземпляр состояния сессии (лениво загружается).</summary>
        SessionState Current { get; }

        /// <summary>Планирует сохранение с дебаунсом. Вызывать с UI-потока.</summary>
        void RequestSave();

        /// <summary>Синхронное сохранение — для завершения работы.</summary>
        void SaveNow();
    }
}
