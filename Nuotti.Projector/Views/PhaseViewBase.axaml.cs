using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Nuotti.Projector.Models;
using Nuotti.Contracts.V1.Model;
using Nuotti.Projector.Presentation;
using Nuotti.Projector.Services;

namespace Nuotti.Projector.Views;

public abstract partial class PhaseViewBase : UserControl
{
    protected readonly ResponsiveTypographyService TypographyService = new();

    protected PhaseViewBase()
    {
        InitializeComponent();
        // Subscribe to size changes to update font sizes
        SizeChanged += OnSizeChanged;
    }

    /// <summary>
    /// Realise a ViewSpec into controls. Views bind; they do not derive.
    /// </summary>
    public abstract void Apply(ViewSpec spec);

    protected virtual void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Gets the window size by traversing up the visual tree.
    /// </summary>
    protected WindowSize GetWindowSize()
    {
        var current = (Control?)this;
        while (current != null)
        {
            if (current is Window window)
            {
                return new WindowSize(window.Bounds.Width, window.Bounds.Height);
            }
            current = current.Parent as Control;
        }

        // Fallback to control's bounds if window not found
        return new WindowSize(Bounds.Width, Bounds.Height);
    }

    /// <summary>
    /// Called when the view size changes. Override to update font sizes.
    /// </summary>
    protected virtual void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveFontSizes();
    }

    /// <summary>
    /// Called when the view is attached to the visual tree. Override to update font sizes.
    /// </summary>
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Update font sizes once attached to get accurate window size
        UpdateResponsiveFontSizes();
    }

    /// <summary>
    /// Override this method to update font sizes for controls in derived views.
    /// </summary>
    protected virtual void UpdateResponsiveFontSizes()
    {
        // Override in derived classes
    }
}
