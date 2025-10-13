using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// Setlist manifest uploaded by the Performer to describe the session's songs.
/// Contains metadata only (no audio bytes).
/// </summary>
public sealed class SetlistManifest
{
    [JsonPropertyName("songs")]
    public List<SongEntry> Songs { get; init; } = new();

    public sealed class SongEntry : IValidatableObject
    {
        [Required]
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("artist")]
        public string? Artist { get; init; }

        [Required]
        [JsonPropertyName("file")] // URL or path reference to the audio file
        public string File { get; init; } = string.Empty;

        [JsonPropertyName("hints")]
        public List<string> Hints { get; init; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Title))
                yield return new ValidationResult("Title is required.", new[] { nameof(Title) });

            if (string.IsNullOrWhiteSpace(File))
            {
                yield return new ValidationResult("File path or URL is required.", new[] { nameof(File) });
                yield break;
            }

            // Accept either existing file path or well-formed http/https URL
            if (System.IO.File.Exists(File)) yield break;
            if (Uri.TryCreate(File, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme is not ("http" or "https"))
                    yield return new ValidationResult("Only http/https URLs are allowed.", new[] { nameof(File) });
            }
            else
            {
                yield return new ValidationResult("File path does not exist and URL is not valid.", new[] { nameof(File) });
            }
        }
    }
}
