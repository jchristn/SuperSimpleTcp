namespace Test.Shared;

using Touchstone.Core;
using Test.Shared.Suites;

public static class SuperSimpleTcpTestSuites
{
    public static IReadOnlyList<TestSuiteDescriptor> All { get; } =
        new List<TestSuiteDescriptor>
        {
            ConstructorSuites.ClientConstructorSuite(),
            ConstructorSuites.ServerConstructorSuite(),
            ConfigurationSuites.ClientSettingsSuite(),
            ConfigurationSuites.ServerSettingsSuite(),
            ConfigurationSuites.KeepaliveSuite(),
            ConfigurationSuites.StatisticsSuite(),
            RuntimeSuites.ClientSendValidationSuite(),
            RuntimeSuites.ServerSendValidationSuite(),
            RuntimeSuites.ConnectivitySuite(),
            RuntimeSuites.EventSuite(),
            RuntimeSuites.AsyncConnectivitySuite(),
            RuntimeSuites.SslSuite(),
            RuntimeSuites.TimeoutAndBehaviorSuite(),
        };
}
