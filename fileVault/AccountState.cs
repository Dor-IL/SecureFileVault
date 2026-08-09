namespace fileVault
{
    enum AccountState { Active, Locked, NotFound }

    static class AccountStateExtensions
    {
        public static (bool allowed, string message) Describe(this AccountState state)
        {
            switch (state)
            {
                case AccountState.Active:
                    return (true, null);
                case AccountState.Locked:
                    return (false, "Your account has been locked by an administrator.");
                default:
                    return (false, "Your account has been deleted by an administrator.");
            }
        }
    }
}
