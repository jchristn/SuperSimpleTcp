namespace SuperSimpleTcp.UnitTest
{
    using System.Collections.Generic;

    /// <summary>
    /// One captured metric measurement.
    /// </summary>
    public sealed class CapturedMeasurement
    {
        /// <summary>
        /// Instrument name.
        /// </summary>
        public string Instrument { get; }

        /// <summary>
        /// Instrument unit.
        /// </summary>
        public string? Unit { get; }

        /// <summary>
        /// Measured value.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Tags, stringified.
        /// </summary>
        public IReadOnlyDictionary<string, string?> Tags { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CapturedMeasurement(string instrument, string? unit, double value, IReadOnlyDictionary<string, string?> tags)
        {
            Instrument = instrument;
            Unit = unit;
            Value = value;
            Tags = tags;
        }

        /// <summary>
        /// Tag value or null.
        /// </summary>
        public string? Tag(string key)
        {
            return Tags.TryGetValue(key, out string? value) ? value : null;
        }
    }
}
