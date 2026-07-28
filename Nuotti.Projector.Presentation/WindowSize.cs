namespace Nuotti.Projector.Presentation;

/// <summary>
/// Window dimensions in device-independent pixels.
/// </summary>
/// <remarks>
/// Replaces Avalonia.Size in the presentation layer. Avalonia.Size is the only reason
/// PhasePresenter could not be referenced without a UI framework; keeping our own two-field
/// record means the layer stays testable and simulatable without a window.
/// </remarks>
public readonly record struct WindowSize(double Width, double Height);
