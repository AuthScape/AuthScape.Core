namespace Authscape.Reporting.Models.ReportContent
{
    public class ComboChartContent : BaseReportContent
    {
        /// <summary>
        /// X-axis labels (e.g., ["Month", "Jan", "Feb", "Mar", ...])
        /// First item is the axis label, rest are data point labels
        /// </summary>
        public List<string> XAxis { get; set; }

        /// <summary>
        /// Data series for the combo chart
        /// </summary>
        public List<ComboChartDataPoint> DataPoints { get; set; }

        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Default series type: "bars", "line", "area", "steppedArea"
        /// </summary>
        public string DefaultSeriesType { get; set; } = "bars";
    }

    public class ComboChartDataPoint
    {
        /// <summary>
        /// Series label (e.g., "Sales", "Expenses", "Profit")
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Data values for each X-axis point
        /// </summary>
        public List<double> Data { get; set; }

        /// <summary>
        /// Series type for this data: "bars", "line", "area", "steppedArea"
        /// If null, uses the chart's default series type
        /// </summary>
        public string SeriesType { get; set; }
    }
}
