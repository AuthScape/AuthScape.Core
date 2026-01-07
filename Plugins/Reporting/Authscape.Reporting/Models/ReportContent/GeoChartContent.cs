namespace Authscape.Reporting.Models.ReportContent
{
    public class GeoChartContent : BaseReportContent
    {
        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Geographic data points
        /// </summary>
        public List<GeoDataPoint> DataPoints { get; set; }

        /// <summary>
        /// Region to display (e.g., "world", "US", "Europe", "150" for country codes)
        /// </summary>
        public string Region { get; set; } = "world";

        /// <summary>
        /// Display mode: "auto", "regions", "markers", "text"
        /// </summary>
        public string DisplayMode { get; set; } = "auto";

        /// <summary>
        /// Value label for the legend
        /// </summary>
        public string ValueLabel { get; set; } = "Value";
    }

    public class GeoDataPoint
    {
        /// <summary>
        /// Location identifier (country name, region code, city, or lat/lng)
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Value for this location
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Optional label for tooltip
        /// </summary>
        public string Label { get; set; }
    }
}
