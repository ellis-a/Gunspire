namespace Gunspire
{
    /// <summary>
    /// A single additive-or-multiplicative change to a derived <see cref="Attr"/>.
    /// The instance doubles as the removal handle, so whoever added it can take it back.
    /// </summary>
    public class StatModifier
    {
        public Attr Attr;
        public float Value;
        public bool IsPercent;
        public object Source;
        public string Label;

        public static StatModifier Flat(Attr attr, float value, object source = null, string label = null)
            => new StatModifier { Attr = attr, Value = value, IsPercent = false, Source = source, Label = label };

        public static StatModifier Percent(Attr attr, float value, object source = null, string label = null)
            => new StatModifier { Attr = attr, Value = value, IsPercent = true, Source = source, Label = label };

        public override string ToString()
        {
            string v = IsPercent ? (Value >= 0 ? "+" : "") + (Value * 100f).ToString("0") + "%"
                                 : (Value >= 0 ? "+" : "") + Value.ToString("0.##");
            return $"{v} {Attr}";
        }
    }
}
