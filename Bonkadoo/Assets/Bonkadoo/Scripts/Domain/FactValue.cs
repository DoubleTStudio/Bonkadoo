using System;
using System.Globalization;

namespace Bonkadoo.Domain
{
    public enum FactKind { Bool, Int, String }

    /// <summary>Typed flag value. Immutable; compares by kind and value.</summary>
    public readonly struct FactValue : IEquatable<FactValue>
    {
        public FactKind Kind { get; }
        private readonly bool _bool;
        private readonly int _int;
        private readonly string _string;

        private FactValue(FactKind kind, bool b, int i, string s)
        {
            Kind = kind;
            _bool = b;
            _int = i;
            _string = s;
        }

        public static FactValue Of(bool value) => new FactValue(FactKind.Bool, value, 0, null);
        public static FactValue Of(int value) => new FactValue(FactKind.Int, false, value, null);
        public static FactValue Of(string value) =>
            new FactValue(FactKind.String, false, 0, value ?? throw new ArgumentNullException(nameof(value)));

        public bool AsBool => Kind == FactKind.Bool ? _bool : throw new InvalidOperationException($"Fact is {Kind}");
        public int AsInt => Kind == FactKind.Int ? _int : throw new InvalidOperationException($"Fact is {Kind}");
        public string AsString => Kind == FactKind.String ? _string : throw new InvalidOperationException($"Fact is {Kind}");

        public bool Equals(FactValue other)
        {
            if (Kind != other.Kind) return false;
            switch (Kind)
            {
                case FactKind.Bool: return _bool == other._bool;
                case FactKind.Int: return _int == other._int;
                default: return string.Equals(_string, other._string, StringComparison.Ordinal);
            }
        }

        public override bool Equals(object obj) => obj is FactValue other && Equals(other);

        public override int GetHashCode()
        {
            switch (Kind)
            {
                case FactKind.Bool: return _bool ? 1 : 2;
                case FactKind.Int: return _int * 31 + 3;
                default: return StringComparer.Ordinal.GetHashCode(_string);
            }
        }

        /// <summary>Invariant text form used in event payloads.</summary>
        public string Serialize()
        {
            switch (Kind)
            {
                case FactKind.Bool: return _bool ? "true" : "false";
                case FactKind.Int: return _int.ToString(CultureInfo.InvariantCulture);
                default: return _string;
            }
        }

        public static FactValue Deserialize(FactKind kind, string text)
        {
            switch (kind)
            {
                case FactKind.Bool: return Of(text == "true");
                case FactKind.Int: return Of(int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));
                default: return Of(text);
            }
        }

        public override string ToString() => $"{Kind}:{Serialize()}";
    }
}
