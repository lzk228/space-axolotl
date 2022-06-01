namespace Content.Server.StationRecords;

[RegisterComponent]
public sealed class StationRecordKeyStorageComponent : Component
{
    /// <summary>
    ///     The key stored in this component.
    /// </summary>
    public StationRecordKey? Key;
}
