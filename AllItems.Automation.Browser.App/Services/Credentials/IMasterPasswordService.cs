namespace AllItems.Automation.Browser.App.Services.Credentials;

public interface IMasterPasswordService
{
    bool EnsureUnlockedBeforeRun();
}

public sealed class NullMasterPasswordService : IMasterPasswordService
{
    public bool EnsureUnlockedBeforeRun()
    {
        return true;
    }
}
