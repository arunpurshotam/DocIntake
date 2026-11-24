using DocIntake.Core.Domain;

namespace DocIntake.Core.Validation;

/// <summary>One business check on extracted fields.</summary>
public interface IValidationRule
{
    /// <summary>Returns a message for each problem found; an empty list means the rule passed.</summary>
    IReadOnlyList<string> Validate(ExtractionResult result);
}
