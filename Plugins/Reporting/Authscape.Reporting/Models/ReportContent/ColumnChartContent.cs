namespace Authscape.Reporting.Models.ReportContent
{
    public class ColumnChartContent : BaseReportContent
    {
        /// <summary>
        /// X-axis labels (e.g., ["Category", "Q1", "Q2", "Q3", "Q4"])
        /// First item is the axis label, rest are data point labels
        /// </summary>
        public List<string> XAxis { get; set; }

        /// <summary>
        /// Data series for the column chart
        /// </summary>
        public List<ColumnChartDataPoint> DataPoints { get; set; }

        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Stack the columns (default: false)
        /// </summary>
        public bool IsStacked { get; set; } = false;
    }

    public class ColumnChartDataPoint
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