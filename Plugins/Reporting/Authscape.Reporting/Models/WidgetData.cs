using Authscape.Models.Reporting;

namespace Authscape.Reporting.Models
{
    public class WidgetData
    {
        public string Name { get; set; }
        public string? WidgetId { get; set; }

        // Grid positioning
        public int Row { get; set; }
        public int Column { get; set; }
        public int ColumnSpan { get; set; }
        public int RowSpan { get; set; }

        // Report data (same structure as ReportData)
        public IEnumerable<object> Content { get; set; }
        public IEnumerable<object> Columns { get; set; }
        public ReportType ReportType { get; set; }
        public Dictionary<string, object> Options { get; set; }
    }
}
