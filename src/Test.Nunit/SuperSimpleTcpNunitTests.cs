namespace Test.Nunit;

using global::NUnit.Framework;

using Test.Shared;
using Touchstone.Core;
using Touchstone.NunitAdapter;

[TestFixture]
public sealed class SuperSimpleTcpNunitTests : TouchstoneNunitBase
{
    protected override IReadOnlyList<TestSuiteDescriptor> Suites
    {
        get { return SuperSimpleTcpTestSuites.All; }
    }

    [Test]
    public Task RunTest()
    {
        return RunAllAsync(CancellationToken.None);
    }
}
