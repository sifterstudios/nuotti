namespace Nuotti.Contracts.V1.Design;

/// <summary>
/// Design tokens for consistent spacing across all Nuotti applications.
/// </summary>
public static class SpacingTokens
{
    /// <summary>
    /// Extra small spacing: 4px
    /// </summary>
    public const int Xs = 4;
    
    /// <summary>
    /// Small spacing: 8px
    /// </summary>
    public const int Sm = 8;
    
    /// <summary>
    /// Medium spacing: 16px
    /// </summary>
    public const int Md = 16;
    
    /// <summary>
    /// Large spacing: 24px
    /// </summary>
    public const int Lg = 24;
    
    /// <summary>
    /// Extra large spacing: 32px
    /// </summary>
    public const int Xl = 32;
    
    /// <summary>
    /// Extra extra large spacing: 48px
    /// </summary>
    public const int Xxl = 48;
    
    /// <summary>
    /// Gets spacing value as a pixel string for CSS/XAML usage.
    /// </summary>
    public static string GetPx(int value) => $"{value}px";
    
    /// <summary>
    /// Gets spacing value as a rem string (assuming 16px base) for CSS usage.
    /// </summary>
    public static string GetRem(int value) => $"{(value / 16.0):F2}rem";
}
