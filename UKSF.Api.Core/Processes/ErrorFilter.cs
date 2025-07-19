using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Processes;

public record ProcessErrorHandlingConfig
{
    public IReadOnlyList<string> ErrorExclusions { get; init; } = [];
    public string IgnoreErrorGateOpen { get; init; } = "";
    public string IgnoreErrorGateClose { get; init; } = "";
}

/// <summary>
///     Handles filtering of errors based on exclusion rules and ignore gates
/// </summary>
public class ErrorFilter(ProcessErrorHandlingConfig errorHandlingConfig)
{
    private bool _ignoreErrors;

    /// <summary>
    ///     Determines if an error should be ignored based on the configured rules
    /// </summary>
    public bool ShouldIgnoreError(string errorText)
    {
        if (string.IsNullOrEmpty(errorText))
        {
            return true;
        }

        // Check for ignore gate close
        if (!string.IsNullOrEmpty(errorHandlingConfig.IgnoreErrorGateClose) && errorText.ContainsIgnoreCase(errorHandlingConfig.IgnoreErrorGateClose))
        {
            _ignoreErrors = false;
            return true;
        }

        // If currently ignoring errors, continue to ignore
        if (_ignoreErrors)
        {
            return true;
        }

        // Check for ignore gate open
        if (!string.IsNullOrEmpty(errorHandlingConfig.IgnoreErrorGateOpen) && errorText.ContainsIgnoreCase(errorHandlingConfig.IgnoreErrorGateOpen))
        {
            _ignoreErrors = true;
            return true;
        }

        // Check error exclusions
        if (errorHandlingConfig.ErrorExclusions.Any(errorText.ContainsIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Resets the ignore state
    /// </summary>
    public void Reset()
    {
        _ignoreErrors = false;
    }
}
