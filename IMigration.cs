namespace PostgresRefactorSoftDelete;

public interface IMigration
{
    static abstract string Step1 { get; }
    static abstract string Step2 { get; }
    static abstract string Step3 { get; }
    static abstract string SelectDeletedItemIds { get; }
    static abstract string SelectDeletedCartIds { get; }
    static abstract string SelectDeletedCartItemIds { get; }
}
