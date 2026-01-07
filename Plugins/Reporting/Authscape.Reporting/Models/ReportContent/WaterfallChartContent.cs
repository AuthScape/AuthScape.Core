namespace Authscape.Reporting.Models.ReportContent
{
    public class WaterfallChartContent : BaseReportContent
    {
        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Data points for the waterfall chart
        /// </summary>
        public List<WaterfallDataPoint> DataPoints { get; set; }

        /// <summary>
        /// Show connecting lines between bars
        /// </summary>
        public bool ShowConnectorLines { get; set; } = true;

        /// <summary>
        /// Starting value label (e.g., "Starting Balance")
        /// </summary>
        public string StartLabel { get; set; } = "Start";

        /// <summary>
        /// Ending value label (e.g., "Final Balance")
        /// </summary>
        public string EndLabel { get; set; } = "End";
    }

    public class WaterfallDataPoint
    {
        /// <summary>
        /// Category label (e.g., "Revenue", "Expenses")
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Value (positive for increase, negative for decrease)
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Is this a subtotal/total bar? (displays cumulative value)
        /// </summary>
        public bool IsTotal { get; set; } = false;
    }
}
