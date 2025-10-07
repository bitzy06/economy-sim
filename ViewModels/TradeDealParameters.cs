namespace Economy_sim
{
    public class TradeDealParameters
    {
        public bool IsExport { get; set; }
        public string FromCountry { get; set; } = string.Empty;
        public string ToCountry { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public double Price { get; set; }
        public int Duration { get; set; } = 1;
        public TariffType TariffType { get; set; } = TariffType.None;
        public double TariffRate { get; set; }
    }
}
