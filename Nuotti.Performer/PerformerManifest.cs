using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Nuotti.Performer;

public sealed class PerformerManifest
{
    [JsonPropertyName("songs")]
    public List<SongEntry> Songs { get; set; } = new();

    public sealed class SongEntry : IValidatableObject
    {
        [Required]
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("artist")]
        public string? Artist { get; set; }

        [JsonPropertyName("bpm")]
        public int? Bpm { get; set; }

        // Optional SHA-256 of the audio content when imported into local blob store
        [JsonPropertyName("hash")]
        public string? Hash { get; set; }

        [Required]
        [JsonPropertyName("file")] // URL or Path
        public string File { get; set; } = string.Empty;

        [JsonPropertyName("hints")]
        public List<string> Hints { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Title))
                yield return new ValidationResult("Title is required.", new[] { nameof(Title) });

            if (string.IsNullOrWhiteSpace(File))
            {
                yield return new ValidationResult("File path or URL is required.", new[] { nameof(File) });
                yield break;
            }

            // Validate as either an existing file path or a well-formed http/https URL
            if (System.IO.File.Exists(File))
            {
                yield break;
            }

            if (Uri.TryCreate(File, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme is not ("http" or "https"))
                {
                    yield return new ValidationResult("Only http/https URLs are allowed.", new[] { nameof(File) });
                }
            }
            else
            {
                yield return new ValidationResult("File path does not exist and URL is not valid.", new[] { nameof(File) });
            }
        }
    }
}
