namespace Football.Core
{
    public interface IFootballService
    {
        void Initialize();
        void Shutdown();
        bool IsInitialized { get; }
    }
}
