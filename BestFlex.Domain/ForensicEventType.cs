namespace BestFlex.Domain
{
    public enum ForensicEventType
    {
        LoginSuccess,
        LoginFailure,
        AuthorizationFailure,
        DataIntegrityFailure,
        ReadOnlyModeEntered,
        BackupCreated,
        BackupFailed,
        RestoreSimulationFailed,
        AccountingPost,
        SaleCommitted,
        SystemStartup,
        SystemShutdown,
        UnexpectedException,
        Critical,
        Error
    }
}
