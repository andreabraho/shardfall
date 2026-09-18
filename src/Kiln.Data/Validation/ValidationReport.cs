namespace Kiln.Data.Validation;

public enum Severity
{
    Warning,
    Error,
}

/// <summary>
/// One validation finding. <see cref="Where"/> names the file a designer must open and
/// <see cref="Fix"/> says what to do — a validator that only says "invalid" wastes the time
/// it was built to save.
/// </summary>
public sealed record Finding(Severity Severity, string Rule, string Where, string Message, string? Fix = null)
{
    public override string ToString()
    {
        var head = $"{(Severity == Severity.Error ? "ERROR" : "warn ")} [{Rule}] {Where}: {Message}";
        return Fix is null ? head : $"{head}\n        fix: {Fix}";
    }
}

public sealed class ValidationReport
{
    private readonly List<Finding> _findings = [];

    public IReadOnlyList<Finding> Findings => _findings;
    public int ErrorCount => _findings.Count(f => f.Severity == Severity.Error);
    public int WarningCount => _findings.Count(f => f.Severity == Severity.Warning);
    public bool HasErrors => ErrorCount > 0;

    public void Error(string rule, string where, string message, string? fix = null) =>
        _findings.Add(new Finding(Severity.Error, rule, where, message, fix));

    public void Warn(string rule, string where, string message, string? fix = null) =>
        _findings.Add(new Finding(Severity.Warning, rule, where, message, fix));

    public void AddRange(IEnumerable<Finding> findings) => _findings.AddRange(findings);

    public string Format()
    {
        if (_findings.Count == 0) return "Content validation passed with no findings.";

        var lines = _findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Rule, StringComparer.Ordinal)
            .ThenBy(f => f.Where, StringComparer.Ordinal)
            .Select(f => "  " + f.ToString());

        return string.Join("\n", lines);
    }
}
