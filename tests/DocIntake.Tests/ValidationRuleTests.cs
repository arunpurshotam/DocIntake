using DocIntake.Core;
using DocIntake.Core.Validation;

namespace DocIntake.Tests;

public class ValidationRuleTests
{
    private static ExtractionResult ValidFields(Action<Dictionary<string, string>>? change = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["EmployeeName"] = "Jane Sample",
            ["EmployerId"] = "00-0000000",
            ["Wages"] = "52000.00",
            ["Withholding"] = "7800.00",
        };
        change?.Invoke(fields);
        return new ExtractionResult(fields, 0.95);
    }

    // --- RequiredFieldsRule ---

    [Fact]
    public void RequiredFields_AllPresent_NoIssues()
    {
        Assert.Empty(new RequiredFieldsRule().Validate(ValidFields()));
    }

    [Theory]
    [InlineData("EmployeeName")]
    [InlineData("EmployerId")]
    [InlineData("Wages")]
    public void RequiredFields_Missing_ReportsThatField(string field)
    {
        var issues = new RequiredFieldsRule().Validate(ValidFields(f => f.Remove(field)));

        var issue = Assert.Single(issues);
        Assert.Contains(field, issue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RequiredFields_Blank_IsReported(string blank)
    {
        var issues = new RequiredFieldsRule().Validate(ValidFields(f => f["EmployeeName"] = blank));

        Assert.Contains("EmployeeName", Assert.Single(issues));
    }

    [Fact]
    public void RequiredFields_WithholdingIsOptional()
    {
        Assert.Empty(new RequiredFieldsRule().Validate(ValidFields(f => f.Remove("Withholding"))));
    }

    // --- WithholdingRule ---

    [Fact]
    public void Withholding_LessThanWages_NoIssues()
    {
        Assert.Empty(new WithholdingRule().Validate(ValidFields()));
    }

    [Fact]
    public void Withholding_EqualToWages_NoIssues()
    {
        Assert.Empty(new WithholdingRule().Validate(ValidFields(f => f["Withholding"] = "52000.00")));
    }

    [Fact]
    public void Withholding_GreaterThanWages_IsReported()
    {
        var issues = new WithholdingRule().Validate(ValidFields(f => f["Withholding"] = "60000.00"));

        Assert.Contains("greater than Wages", Assert.Single(issues));
    }

    [Fact]
    public void Withholding_ComparesNumbersNotText()
    {
        // As text "9000" > "10000"; as numbers it is not.
        var issues = new WithholdingRule().Validate(ValidFields(f =>
        {
            f["Wages"] = "10,000.00";
            f["Withholding"] = "9000";
        }));

        Assert.Empty(issues);
    }

    [Fact]
    public void Withholding_NotANumber_IsReported()
    {
        var issues = new WithholdingRule().Validate(ValidFields(f => f["Withholding"] = "abc"));

        Assert.Contains("not a valid amount", Assert.Single(issues));
    }

    [Fact]
    public void Withholding_MissingValues_AreLeftToRequiredFieldsRule()
    {
        Assert.Empty(new WithholdingRule().Validate(ValidFields(f => f.Remove("Withholding"))));
        Assert.Empty(new WithholdingRule().Validate(ValidFields(f => f.Remove("Wages"))));
    }

    // --- Validator ---

    [Fact]
    public void Validator_CollectsIssuesFromAllRules()
    {
        var validator = new Validator([new RequiredFieldsRule(), new WithholdingRule()]);

        var issues = validator.Validate(ValidFields(f =>
        {
            f.Remove("EmployerId");
            f["Withholding"] = "99999.00";
        }));

        Assert.Equal(2, issues.Count);
    }
}
