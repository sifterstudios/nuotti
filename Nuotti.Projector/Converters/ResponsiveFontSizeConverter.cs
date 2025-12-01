using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia;
using Nuotti.Projector.Services;

namespace Nuotti.Projector.Converters;

/// <summary>
/// Enum for text type categories to simplify font size binding.
/// </summary>
public enum TextType
{
    Headline,
    Question,
    Option,
    SongTitle,
    SongArtist,
    Body,
    PhaseIcon,
    PhaseTitle,
    ScorePosition,
    ScoreName,
    ScoreValue
}

/// <summary>
/// Value converter for responsive font sizing in XAML.
/// Binds to window size and calculates appropriate font size based on text type.
/// </summary>
public class ResponsiveFontSizeConverter : IValueConverter
{
    private static readonly ResponsiveTypographyService _typographyService = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return 16.0; // Default fallback
        
        // Get window size
        Size windowSize;
        if (value is Window window)
        {
            windowSize = window.Bounds.Size;
        }
        else if (value is Size size)
        {
            windowSize = size;
        }
        else if (value is double width && parameter is double height)
        {
            windowSize = new Size(width, height);
        }
        else
        {
            return 16.0; // Default fallback
        }
        
        // Get text type from parameter
        TextType textType = TextType.Body;
        if (parameter is TextType type)
        {
            textType = type;
        }
        else if (parameter is string typeString && Enum.TryParse<TextType>(typeString, true, out var parsedType))
        {
            textType = parsedType;
        }
        
        // Get safe area margin (default 5%)
        double safeAreaMargin = 0.05;
        if (parameter is Tuple<TextType, double> tuple)
        {
            textType = tuple.Item1;
            safeAreaMargin = tuple.Item2;
        }
        
        // Calculate font size based on text type
        return textType switch
        {
            TextType.Headline => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.HeadlineMin,
                ResponsiveTypographyService.FontSizes.HeadlineMax,
                windowSize,
                safeAreaMargin),
            TextType.Question => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.QuestionMin,
                ResponsiveTypographyService.FontSizes.QuestionMax,
                windowSize,
                safeAreaMargin),
            TextType.Option => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.OptionMin,
                ResponsiveTypographyService.FontSizes.OptionMax,
                windowSize,
                safeAreaMargin),
            TextType.SongTitle => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.SongTitleMin,
                ResponsiveTypographyService.FontSizes.SongTitleMax,
                windowSize,
                safeAreaMargin),
            TextType.SongArtist => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.SongArtistMin,
                ResponsiveTypographyService.FontSizes.SongArtistMax,
                windowSize,
                safeAreaMargin),
            TextType.PhaseIcon => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.PhaseIconMin,
                ResponsiveTypographyService.FontSizes.PhaseIconMax,
                windowSize,
                safeAreaMargin),
            TextType.PhaseTitle => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.PhaseTitleMin,
                ResponsiveTypographyService.FontSizes.PhaseTitleMax,
                windowSize,
                safeAreaMargin),
            TextType.ScorePosition => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.ScorePositionMin,
                ResponsiveTypographyService.FontSizes.ScorePositionMax,
                windowSize,
                safeAreaMargin),
            TextType.ScoreName => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.ScoreNameMin,
                ResponsiveTypographyService.FontSizes.ScoreNameMax,
                windowSize,
                safeAreaMargin),
            TextType.ScoreValue => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.ScoreValueMin,
                ResponsiveTypographyService.FontSizes.ScoreValueMax,
                windowSize,
                safeAreaMargin),
            _ => _typographyService.CalculateFontSizeFromWindow(
                ResponsiveTypographyService.FontSizes.BodyMin,
                ResponsiveTypographyService.FontSizes.BodyMax,
                windowSize,
                safeAreaMargin)
        };
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // One-way converter, no conversion back
        throw new NotImplementedException();
    }
}

