namespace SafeRideKids.Motorista.Maui.Services;

public interface IHapticsService
{
    void Light();
    void Success();
    void Warning();
}

// Wrapper sobre HapticFeedback.Default. Silencia exceptions em plataformas sem suporte.
public sealed class HapticsService : IHapticsService
{
    public void Light()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
    }

    public void Success()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
    }

    public void Warning()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
    }
}
