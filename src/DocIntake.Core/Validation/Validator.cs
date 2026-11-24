using DocIntake.Core.Domain;

namespace DocIntake.Core.Validation;

/// <summary>Runs every validation rule and collects all of their issues.</summary>
public class Validator(IEnumerable<IValidationRule> rules)
{
    private readonly IReadOnlyList<IValidationRule> _rules = rules.ToList();

    public IReadOnlyList<string> Validate(ExtractionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return _rules.SelectMany(rule => rule.Validate(result)).ToList();
    }
}
