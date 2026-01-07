namespace Authscape.Reporting.Models.ReportContent
{
    public class AnnotationChartContent : BaseReportContent
    {
        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Data series for the annotation chart
        /// </summary>
        public List<AnnotationDataSeries> Series { get; set; }

        /// <summary>
        /// Display annotations table below chart
        /// </summary>
        public bool DisplayAnnotations { get; set; } = true;
    }

    public class AnnotationDataSeries
    {
        /// <summary>
        /// Series label (e.g., "Stock Price")
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Data points with date, value, and optional annotations
        /// </summary>
        public List<AnnotationDataPoint> DataPoints { get; set; }
    }

    public class AnnotationDataPoint
    {
        /// <summary>
        /// Date/time for this data point
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Value at this date
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Short annotation title (optional)
        /// </summary>
        public string AnnotationTitle { get; set; }

        /// <summary>
        /// Full annotation text (optional)
        /// </summary>
        public string AnnotationText { get; set; }
    }
}