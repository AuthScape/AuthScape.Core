namespace Authscape.Reporting.Models.ReportContent
{
    public class LineChartContent : BaseReportContent
    {
        /// <summary>
        /// X-axis labels (e.g., ["Month", "Jan", "Feb", "Mar", ...])
        /// First item is the axis label, rest are data point labels
        /// </summary>
        public List<string> XAxis { get; set; }

        /// <summary>
        /// Data series for the line chart
        /// </summary>
        public List<LineChartDataPoint> DataPoints { get; set; }

        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Enable curved lines (default: false)
        /// </summary>
        public bool CurveLines { get; set; } = false;
    }

    public class LineChartDataPoint
    {
        /// <summary>
        /// Series label (e.g., "Sales", "Revenue")
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Data values for each X-axis point
        /// </summary>
        public List<double> Data { get; set; }
    }
}
