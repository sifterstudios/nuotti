namespace Nuotti.Audience.Services;

public class NameValidationService
{
    private static readonly HashSet<string> ProfanityList = new(StringComparer.OrdinalIgnoreCase)
    {
        // Basic profanity filter - in production, this would be more comprehensive
        "damn", "hell", "crap", "stupid", "idiot", "moron", "dumb", "suck", "hate"
        // Add more words as needed
    };

    private static readonly HashSet<string> UsedNames = new(StringComparer.OrdinalIgnoreCase);

    public ValidationResult ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationResult.Success();
        }

        var trimmedName = name.Trim();

        // Length validation
        if (trimmedName.Length < 2)
        {
            return ValidationResult.Error("Name must be at least 2 characters long");
        }

        if (trimmedName.Length > 20)
        {
            return ValidationResult.Error("Name must be 20 characters or less");
        }

        // Profanity check
        if (ContainsProfanity(trimmedName))
        {
            var cleanName = FilterProfanity(trimmedName);
            return ValidationResult.Warning($"Name contains inappropriate content. Suggested: {cleanName}");
        }

        // Duplicate check
        if (IsNameTaken(trimmedName))
        {
            var suggestedName = GenerateUniqueName(trimmedName);
            return ValidationResult.Warning($"Name already taken. Suggested: {suggestedName}");
        }

        return ValidationResult.Success();
    }

    public string ReserveName(string name)
    {
        var cleanName = FilterProfanity(name.Trim());
        var uniqueName = GenerateUniqueName(cleanName);
        UsedNames.Add(uniqueName);
        return uniqueName;
    }

    public void ReleaseName(string name)
    {
        UsedNames.Remove(name);
    }

    private bool ContainsProfanity(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Any(word => ProfanityList.Contains(word));
    }

    private string FilterProfanity(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filteredWords = words.Select(word => 
            ProfanityList.Contains(word) ? new string('*', word.Length) : word);
        return string.Join(" ", filteredWords);
    }

    private bool IsNameTaken(string name)
    {
        return UsedNames.Contains(name);
    }

    private string GenerateUniqueName(string baseName)
    {
        if (!IsNameTaken(baseName))
        {
            return baseName;
        }

        for (int i = 2; i <= 99; i++)
        {
            var candidateName = $"{baseName} ({i})";
            if (!IsNameTaken(candidateName))
            {
                return candidateName;
            }
        }

        // Fallback to random suffix if all numbers are taken
        var randomSuffix = Random.Shared.Next(100, 999);
        return $"{baseName} ({randomSuffix})";
    }

    public class ValidationResult
    {
        public bool IsValid { get; init; }
        public string? Message { get; init; }
        public ValidationSeverity Severity { get; init; }

        public static ValidationResult Success() => new() { IsValid = true };
        public static ValidationResult Error(string message) => new() { IsValid = false, Message = message, Severity = ValidationSeverity.Error };
        public static ValidationResult Warning(string message) => new() { IsValid = true, Message = message, Severity = ValidationSeverity.Warning };
    }

    public enum ValidationSeverity
    {
        Error,
        Warning
    }
}
