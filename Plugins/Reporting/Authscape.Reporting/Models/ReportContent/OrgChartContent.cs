namespace Authscape.Reporting.Models.ReportContent
{
    public class OrgChartContent : BaseReportContent
    {
        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Organization nodes
        /// </summary>
        public List<OrgDataPoint> Nodes { get; set; }

        /// <summary>
        /// Allow HTML in node labels
        /// </summary>
        public bool AllowHtml { get; set; } = true;

        /// <summary>
        /// Allow collapsing nodes
        /// </summary>
        public bool AllowCollapse { get; set; } = true;

        /// <summary>
        /// Node size: "small", "medium", "large"
        /// </summary>
        public string Size { get; set; } = "medium";
    }

    public class OrgDataPoint
    {
        /// <summary>
        /// Node ID (unique identifier)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display name (can include HTML if AllowHtml is true)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Parent node ID (null or empty for root)
        /// </summary>
        public string ParentId { get; set; }

        /// <summary>
        /// Tooltip text
        /// </summary>
        public string Tooltip { get; set; }
    }
}
