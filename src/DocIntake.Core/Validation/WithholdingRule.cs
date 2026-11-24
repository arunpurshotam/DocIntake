using System.Globalization;
using DocIntake.Core.Domain;

namespace DocIntake.Core.Validation;

/// <summary>Fails when tax withheld is more than the wages it was withheld from.</summary>
public class WithholdingRule : IValidationRule
{
    public IReadOnlyList<string> Validate(ExtractionResult result)
    {
        var issues = new List<string>();

        // Missing values are RequiredFieldsRule's job (Withholding itself is optional).
        if (!TryGetValue(result, "Wages", out var wagesText) || !TryGetValue(result, "Withholding", out var withholdingText))
        {
            return issues;
        }

        // A value that isn't a number can't be compared, so flag it rather than silently pass.
        var wagesOk = TryParseAmount(wagesText, out var wages);
        var withholdingOk = TryParseAmount(withholdingText, out var withholding);
        if (!wagesOk)
        {
            issues.Add($"Wages '{wagesText}' is not a valid amount.");
        }
        if (!withholdingOk)
        {
            issues.Add($"Withholding '{withholdingText}' is not a valid amount.");
        }

        if (wagesOk && withholdingOk && withholding > wages)
        {
            issues.Add($"Withholding ({withholdingText}) is greater than Wages ({wagesText}).");
        }

        return issues;
    }

    private static bool TryGetValue(ExtractionResult result, string field, out string value)
    {
        if (result.Fields.TryGetValue(field, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            value = raw;
            return true;
        }

        value = "";
        return false;
    }

    // Accepts plain amounts like "52000.00" or "52,000.00"; culture-independent.
    private static bool TryParseAmount(string text, out decimal amount) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
}
