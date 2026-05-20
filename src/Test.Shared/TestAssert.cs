namespace Test.Shared;

using System.Collections;
using System.Runtime.ExceptionServices;

internal static class TestAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message)
    {
        True(!condition, message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(
                message + $" Expected: {expected}. Actual: {actual}.");
    }

    public static void NotEqual<T>(T notExpected, T actual, string message)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
            throw new InvalidOperationException(message + $" Value: {actual}.");
    }

    public static void Null(object? value, string message)
    {
        if (value != null)
            throw new InvalidOperationException(message);
    }

    public static T NotNull<T>(T? value, string message) where T : class
    {
        if (value == null)
            throw new InvalidOperationException(message);

        return value;
    }

    public static void Contains(string expectedSubstring, string actual, string message)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
            throw new InvalidOperationException(
                message + $" Missing substring: {expectedSubstring}.");
    }

    public static void SequenceEqual(
        IEnumerable<byte> expected,
        IEnumerable<byte> actual,
        string message)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException(message);
    }

    public static void Empty(IEnumerable values, string message)
    {
        foreach (object? _ in values)
            throw new InvalidOperationException(message);
    }

    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (ex.GetType() != typeof(TException))
                ExceptionDispatchInfo.Capture(ex).Throw();

            return (TException)ex;
        }

        throw new InvalidOperationException(
            $"Expected exception {typeof(TException).Name} was not thrown.");
    }

    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex.GetType() != typeof(TException))
                ExceptionDispatchInfo.Capture(ex).Throw();

            return (TException)ex;
        }

        throw new InvalidOperationException(
            $"Expected exception {typeof(TException).Name} was not thrown.");
    }
}
