namespace Authscape.Reporting.Models.ReportContent
{
    public class SteppedAreaChartContent : BaseReportContent
    {
        /// <summary>
        /// X-axis labels (e.g., ["Month", "Jan", "Feb", "Mar", ...])
        /// First item is the axis label, rest are data point labels
        /// </summary>
        public List<string> XAxis { get; set; }

        /// <summary>
        /// Data series for the stepped area chart
        /// </summary>
        public List<SteppedAreaDataPoint> DataPoints { get; set; }

        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Stack the areas (default: false)
        /// </summary>
        public bool IsStacked { get; set; } = false;
    }

    public class SteppedAreaDataPoint
    {
        /// <summary>
        /// Series label (e.g., "Inventory Level")
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Data values for each X-axis point
        /// </summary>
        public List<double> Data { get; set; }
    }
}
