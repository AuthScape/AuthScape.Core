namespace Authscape.Reporting.Models.ReportContent
{
    public class SankeyChartContent : BaseReportContent
    {
        /// <summary>
        /// List of connections between nodes in the Sankey diagram
        /// </summary>
        public List<SankeyDataPoint> DataPoints { get; set; }

        /// <summary>
        /// Optional title for the chart
        /// </summary>
        public string Title { get; set; }
    }

    public class SankeyDataPoint
    {
        /// <summary>
        /// Source node name (e.g., "Marketing Budget")
        /// </summary>
        public string From { get; set; }

        /// <summary>
        /// Destination node name (e.g., "Social Media")
        /// </summary>
        public string To { get; set; }

        /// <summary>
        /// Weight/flow value between nodes (determines line thickness)
        /// </summary>
        public decimal Weight { get; set; }
    }
}
