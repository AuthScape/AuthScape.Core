namespace Authscape.Reporting.Models.ReportContent
{
    public class TreeMapChartContent : BaseReportContent
    {
        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Tree map nodes
        /// </summary>
        public List<TreeMapDataPoint> Nodes { get; set; }

        /// <summary>
        /// Show scale legend
        /// </summary>
        public bool ShowScale { get; set; } = true;

        /// <summary>
        /// Maximum depth to display
        /// </summary>
        public int MaxDepth { get; set; } = 1;

        /// <summary>
        /// Header height in pixels
        /// </summary>
        public int HeaderHeight { get; set; } = 15;
    }

    public class TreeMapDataPoint
    {
        /// <summary>
        /// Node identifier/label
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Parent node ID (null or empty for root nodes)
        /// </summary>
        public string Parent { get; set; }

        /// <summary>
        /// Size value (determines rectangle size)
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Color value (optional, for color intensity)
        /// </summary>
        public decimal? ColorValue { get; set; }
    }
}
