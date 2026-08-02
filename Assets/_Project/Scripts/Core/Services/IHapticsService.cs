namespace Core.Services
{
    public enum HapticStrength
    {
        Light,
        Medium,
        Heavy
    }

    public interface IHapticsService
    {
        bool IsEnabled { get; set; }
        void Play(HapticStrength strength);
    }
}
