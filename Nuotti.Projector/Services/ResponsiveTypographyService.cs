using System;
using Avalonia;

namespace Nuotti.Projector.Services;

/// <summary>
/// Service for calculating responsive font sizes based on viewport dimensions and safe area.
/// Implements clamp-like logic to ensure text scales appropriately across different screen sizes.
/// </summary>
public class ResponsiveTypographyService
{
    // Viewport breakpoints (in pixels)
    private const double MinViewportWidth = 1280;  // 720p width
    private const double MaxViewportWidth = 3840;  // 4K width
    private const double MinViewportHeight = 720;  // 720p height
    private const double MaxViewportHeight = 2160; // 4K height
    
    /// <summary>
    /// Calculates a responsive font size using clamp-like logic.
    /// </summary>
    /// <param name="minSize">Minimum font size in pixels</param>
    /// <param name="maxSize">Maximum font size in pixels</param>
    /// <param name="viewportDimension">Current viewport dimension (width or height)</param>
    /// <param name="safeAreaMargin">Safe area margin (0.0 to 1.0, default 0.05 = 5%)</param>
    /// <returns>Calculated font size clamped between minSize and maxSize</returns>
    public double CalculateFontSize(double minSize, double maxSize, double viewportDimension, double safeAreaMargin = 0.05)
    {
        if (minSize >= maxSize)
            return minSize;
        
        // Account for safe area - reduce available viewport
        var availableDimension = viewportDimension * (1.0 - (safeAreaMargin * 2));
        
        // Determine which viewport range to use based on dimension
        var minViewport = viewportDimension <= 1920 ? MinViewportWidth : MinViewportWidth;
        var maxViewport = viewportDimension <= 1920 ? MaxViewportWidth : MaxViewportWidth;
        
        // Clamp viewport dimension to our range
        var clampedViewport = Math.Max(minViewport, Math.Min(maxViewport, availableDimension));
        
        // Linear interpolation: size = min + (max - min) * ((viewport - minViewport) / (maxViewport - minViewport))
        var ratio = (clampedViewport - minViewport) / (maxViewport - minViewport);
        var calculatedSize = minSize + (maxSize - minSize) * ratio;
        
        // Final clamp to ensure we never exceed bounds
        return Math.Max(minSize, Math.Min(maxSize, calculatedSize));
    }
    
    /// <summary>
    /// Calculates font size based on window size, using the smaller dimension for better mobile support.
    /// </summary>
    /// <param name="minSize">Minimum font size in pixels</param>
    /// <param name="maxSize">Maximum font size in pixels</param>
    /// <param name="windowSize">Current window size</param>
    /// <param name="safeAreaMargin">Safe area margin (0.0 to 1.0)</param>
    /// <returns>Calculated font size</returns>
    public double CalculateFontSizeFromWindow(double minSize, double maxSize, Size windowSize, double safeAreaMargin = 0.05)
    {
        // Use the smaller dimension to ensure text fits on both axes
        var dimension = Math.Min(windowSize.Width, windowSize.Height);
        return CalculateFontSize(minSize, maxSize, dimension, safeAreaMargin);
    }
    
    /// <summary>
    /// Calculates font size using width as the primary dimension (for horizontal layouts).
    /// </summary>
    public double CalculateFontSizeFromWidth(double minSize, double maxSize, double width, double safeAreaMargin = 0.05)
    {
        return CalculateFontSize(minSize, maxSize, width, safeAreaMargin);
    }
    
    /// <summary>
    /// Calculates font size using height as the primary dimension (for vertical layouts).
    /// </summary>
    public double CalculateFontSizeFromHeight(double minSize, double maxSize, double height, double safeAreaMargin = 0.05)
    {
        return CalculateFontSize(minSize, maxSize, height, safeAreaMargin);
    }
    
    /// <summary>
    /// Predefined font size ranges for common text types.
    /// </summary>
    public static class FontSizes
    {
        // Headlines (e.g., "Welcome to Nuotti!")
        public const double HeadlineMin = 36;
        public const double HeadlineMax = 72;
        
        // Question text
        public const double QuestionMin = 32;
        public const double QuestionMax = 48;
        
        // Option text
        public const double OptionMin = 18;
        public const double OptionMax = 24;
        
        // Song title
        public const double SongTitleMin = 20;
        public const double SongTitleMax = 32;
        
        // Song artist
        public const double SongArtistMin = 14;
        public const double SongArtistMax = 24;
        
        // Body/secondary text
        public const double BodyMin = 12;
        public const double BodyMax = 20;
        
        // Phase icon (large emoji)
        public const double PhaseIconMin = 48;
        public const double PhaseIconMax = 96;
        
        // Phase title
        public const double PhaseTitleMin = 24;
        public const double PhaseTitleMax = 48;
        
        // Scoreboard position
        public const double ScorePositionMin = 20;
        public const double ScorePositionMax = 32;
        
        // Scoreboard name
        public const double ScoreNameMin = 16;
        public const double ScoreNameMax = 24;
        
        // Scoreboard score
        public const double ScoreValueMin = 20;
        public const double ScoreValueMax = 28;
    }
}

