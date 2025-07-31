using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Processes;

/// <summary>
///     Handles filtering of errors based on exclusion rules and ignore gates
/// </summary>
public class ErrorFilter()
{
    public List<string> ErrorExclusions { get; set; } = [];
    public string IgnoreErrorGateOpen { get; set; } = "";
    public string IgnoreErrorGateClose { get; set; } = "";

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
        if (!string.IsNullOrEmpty(IgnoreErrorGateClose) && errorText.ContainsIgnoreCase(IgnoreErrorGateClose))
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
        if (!string.IsNullOrEmpty(IgnoreErrorGateOpen) && errorText.ContainsIgnoreCase(IgnoreErrorGateOpen))
        {
            _ignoreErrors = true;
            return true;
        }

        // Check error exclusions
        if (ErrorExclusions.Any(errorText.ContainsIgnoreCase))
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
