using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Services;

public class MigrationUtility
{
    private const int Version = 0;
    private readonly IAccountContext _accountContext;
    private readonly IUksfLogger _logger;
    private readonly IMigrationContext _migrationContext;

    public MigrationUtility(IMigrationContext migrationContext, IUksfLogger logger, IAccountContext accountContext)
    {
        _migrationContext = migrationContext;
        _logger = logger;
        _accountContext = accountContext;
    }

    public void Migrate()
    {
        if (_migrationContext.GetSingle(x => x.Version == Version) != null)
        {
            return;
        }

        try
        {
            ExecuteMigration();
            _migrationContext.Add(new() { Version = Version });
            _logger.LogInfo($"Migration version {Version} executed successfully");
        }
        catch (Exception e)
        {
            _logger.LogError(e);
            throw;
        }
    }

    private void ExecuteMigration()
    {
        var accounts = _accountContext.Get();
        foreach (var account in accounts)
        {
            account.Qualifications = new();
            _accountContext.Replace(account);
        }
    }
}
