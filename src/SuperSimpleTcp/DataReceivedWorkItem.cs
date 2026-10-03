namespace SuperSimpleTcp
{
    using System;
    using System.Diagnostics;

    internal sealed class DataReceivedWorkItem
    {
        internal DataReceivedEventArgs Args { get; }

        internal ActivityContext ParentContext { get; }

        internal long EnqueuedTimestamp { get; }

        internal DataReceivedWorkItem(DataReceivedEventArgs args, ActivityContext parentContext, long enqueuedTimestamp)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            Args = args;
            ParentContext = parentContext;
            EnqueuedTimestamp = enqueuedTimestamp;
        }
    }
}
