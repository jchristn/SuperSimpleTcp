namespace Test.Shared;

using Touchstone.Core;

internal static class TestCaseFactory
{
    public static TestCaseDescriptor Sync(
        string suiteId,
        string caseId,
        string displayName,
        Action execute)
    {
        return new TestCaseDescriptor(
            suiteId,
            caseId,
            displayName,
            _ =>
            {
                execute();
                return Task.CompletedTask;
            });
    }

    public static TestCaseDescriptor Async(
        string suiteId,
        string caseId,
        string displayName,
        Func<CancellationToken, Task> executeAsync)
    {
        return new TestCaseDescriptor(suiteId, caseId, displayName, executeAsync);
    }
}
