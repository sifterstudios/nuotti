using Microsoft.JSInterop;

namespace Nuotti.Audience.Services;

public class FeedbackService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<FeedbackService>? _objRef;
    private bool _isInitialized = false;
    
    public bool HapticsEnabled { get; private set; } = true;
    public bool AnimationsEnabled { get; private set; } = true;
    public bool ReducedMotion { get; private set; } = false;

    public FeedbackService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        _objRef = DotNetObjectReference.Create(this);
        
        try
        {
            // Check for reduced motion preference
            ReducedMotion = await _jsRuntime.InvokeAsync<bool>("matchMedia", "(prefers-reduced-motion: reduce)");
            AnimationsEnabled = !ReducedMotion;
            
            // Load user preferences
            await LoadPreferencesAsync();
            
            // Initialize the JavaScript module
            await _jsRuntime.InvokeVoidAsync("nuottiFeedback.initialize", _objRef);
            
            _isInitialized = true;
        }
        catch (Exception)
        {
            // Gracefully handle initialization failures
            _isInitialized = false;
        }
    }

    public async Task TriggerHapticFeedbackAsync(HapticType type = HapticType.Light)
    {
        if (!_isInitialized || !HapticsEnabled) return;
        
        try
        {
            await _jsRuntime.InvokeVoidAsync("nuottiFeedback.haptic", type.ToString().ToLower());
        }
        catch (Exception)
        {
            // Ignore haptic failures - not all devices support it
        }
    }

    public async Task TriggerConfettiAsync(ConfettiType type = ConfettiType.Success)
    {
        if (!_isInitialized || !AnimationsEnabled) return;
        
        try
        {
            await _jsRuntime.InvokeVoidAsync("nuottiFeedback.confetti", type.ToString().ToLower());
        }
        catch (Exception)
        {
            // Ignore animation failures
        }
    }

    public async Task TriggerPulseAnimationAsync(string elementId)
    {
        if (!_isInitialized || !AnimationsEnabled) return;
        
        try
        {
            await _jsRuntime.InvokeVoidAsync("nuottiFeedback.pulse", elementId);
        }
        catch (Exception)
        {
            // Ignore animation failures
        }
    }

    public async Task TriggerShakeAnimationAsync(string elementId)
    {
        if (!_isInitialized || !AnimationsEnabled) return;
        
        try
        {
            await _jsRuntime.InvokeVoidAsync("nuottiFeedback.shake", elementId);
        }
        catch (Exception)
        {
            // Ignore animation failures
        }
    }

    public async Task SetHapticsEnabledAsync(bool enabled)
    {
        HapticsEnabled = enabled;
        await SavePreferencesAsync();
    }

    public async Task SetAnimationsEnabledAsync(bool enabled)
    {
        AnimationsEnabled = enabled && !ReducedMotion; // Respect system preference
        await SavePreferencesAsync();
    }

    private async Task LoadPreferencesAsync()
    {
        try
        {
            var hapticsEnabled = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "nuotti-haptics-enabled");
            if (bool.TryParse(hapticsEnabled, out var haptics))
            {
                HapticsEnabled = haptics;
            }

            var animationsEnabled = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "nuotti-animations-enabled");
            if (bool.TryParse(animationsEnabled, out var animations))
            {
                AnimationsEnabled = animations && !ReducedMotion;
            }
        }
        catch (Exception)
        {
            // Ignore localStorage errors
        }
    }

    private async Task SavePreferencesAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "nuotti-haptics-enabled", HapticsEnabled.ToString());
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "nuotti-animations-enabled", AnimationsEnabled.ToString());
        }
        catch (Exception)
        {
            // Ignore localStorage errors
        }
    }

    [JSInvokable]
    public void OnReducedMotionChanged(bool reducedMotion)
    {
        ReducedMotion = reducedMotion;
        if (reducedMotion)
        {
            AnimationsEnabled = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_objRef != null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("nuottiFeedback.dispose");
            }
            catch (Exception)
            {
                // Ignore disposal errors
            }
            _objRef.Dispose();
        }
    }
}

public enum HapticType
{
    Light,
    Medium,
    Heavy
}

public enum ConfettiType
{
    Success,
    Celebration,
    Fireworks
}
