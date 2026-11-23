namespace DocIntake.Core.Validation;

/// <summary>Fails when a field every wage statement must have is missing or blank.</summary>
public class RequiredFieldsRule : IValidationRule
{
    private static readonly string[] RequiredFields = ["EmployeeName", "EmployerId", "Wages"];

    public IReadOnlyList<string> Validate(ExtractionResult result)
    {
        var issues = new List<string>();

        foreach (var field in RequiredFields)
        {
            if (!result.Fields.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
            {
                issues.Add($"Required field '{field}' is missing or empty.");
            }
        }

        return issues;
    }
}
