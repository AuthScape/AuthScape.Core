namespace Authscape.Reporting.Models.ReportContent
{
    public class GanttChartContent : BaseReportContent
    {
        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Tasks for the Gantt chart
        /// </summary>
        public List<GanttDataPoint> Tasks { get; set; }

        /// <summary>
        /// Show critical path highlighting
        /// </summary>
        public bool ShowCriticalPath { get; set; } = true;

        /// <summary>
        /// Height of each task row in pixels
        /// </summary>
        public int TrackHeight { get; set; } = 30;
    }

    public class GanttDataPoint
    {
        /// <summary>
        /// Unique task ID
        /// </summary>
        public string TaskId { get; set; }

        /// <summary>
        /// Display name for the task
        /// </summary>
        public string TaskName { get; set; }

        /// <summary>
        /// Resource/category name (used for grouping/coloring)
        /// </summary>
        public string Resource { get; set; }

        /// <summary>
        /// Task start date
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Task end date
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Duration in milliseconds (optional, alternative to EndDate)
        /// </summary>
        public long? Duration { get; set; }

        /// <summary>
        /// Percentage complete (0-100)
        /// </summary>
        public int PercentComplete { get; set; }

        /// <summary>
        /// Comma-separated list of task IDs this task depends on
        /// </summary>
        public string Dependencies { get; set; }
    }
}
