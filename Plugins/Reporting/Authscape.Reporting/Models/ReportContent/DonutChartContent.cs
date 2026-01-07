namespace Authscape.Reporting.Models.ReportContent
{
    public class DonutChartContent : BaseReportContent
    {
        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Data points for the donut chart
        /// </summary>
        public List<DonutDataPoint> DataPoints { get; set; }

        /// <summary>
        /// Size of the hole in the center (0.0 to 1.0)
        /// Default is 0.4 (40% hole)
        /// </summary>
        public double PieHole { get; set; } = 0.4;
    }

    public class DonutDataPoint
    {
        /// <summary>
        /// Slice label (e.g., "Category A")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Slice value
        /// </summary>
        public decimal Value { get; set; }
    }
}
