using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Threading.Tasks;

namespace Nuotti.Projector.Services;

public class AnimationService
{
    private readonly TimeSpan _defaultDuration = TimeSpan.FromMilliseconds(300);
    
    public async Task AnimateCounterUpdate(TextBlock counter, int oldValue, int newValue)
    {
        if (oldValue == newValue) return;
        
        try
        {
            // Scale animation for emphasis
            var scaleAnimation = new Animation
            {
                Duration = _defaultDuration,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0.0),
                        Setters = { new Setter(Visual.RenderTransformProperty, new ScaleTransform(1.0, 1.0)) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(0.5),
                        Setters = { new Setter(Visual.RenderTransformProperty, new ScaleTransform(1.2, 1.2)) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1.0),
                        Setters = { new Setter(Visual.RenderTransformProperty, new ScaleTransform(1.0, 1.0)) }
                    }
                }
            };
            
            // Update the text value
            counter.Text = newValue.ToString();
            
            // Run the animation
            await scaleAnimation.RunAsync(counter);
        }
        catch (Exception ex)
        {
            // Fallback to immediate update if animation fails
            counter.Text = newValue.ToString();
            Console.WriteLine($"Animation failed: {ex.Message}");
        }
    }
    
    public async Task AnimateBackgroundChange(Border border, IBrush newBrush)
    {
        try
        {
            // Opacity fade animation
            var fadeAnimation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0.0),
                        Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(0.5),
                        Setters = { new Setter(Visual.OpacityProperty, 0.7) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1.0),
                        Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                    }
                }
            };
            
            // Change background at the midpoint
            _ = Task.Delay(100).ContinueWith(_ => 
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    border.Background = newBrush;
                });
            });
            
            await fadeAnimation.RunAsync(border);
        }
        catch (Exception ex)
        {
            // Fallback to immediate change
            border.Background = newBrush;
            Console.WriteLine($"Background animation failed: {ex.Message}");
        }
    }
    
    public async Task AnimateSlideIn(Control control)
    {
        try
        {
            var slideAnimation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(400),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0.0),
                        Setters = 
                        { 
                            new Setter(Visual.OpacityProperty, 0.0),
                            new Setter(Visual.RenderTransformProperty, new TranslateTransform(0, 20))
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1.0),
                        Setters = 
                        { 
                            new Setter(Visual.OpacityProperty, 1.0),
                            new Setter(Visual.RenderTransformProperty, new TranslateTransform(0, 0))
                        }
                    }
                }
            };
            
            await slideAnimation.RunAsync(control);
        }
        catch (Exception ex)
        {
            // Fallback to immediate show
            control.Opacity = 1.0;
            control.RenderTransform = new TranslateTransform(0, 0);
            Console.WriteLine($"Slide animation failed: {ex.Message}");
        }
    }
}
