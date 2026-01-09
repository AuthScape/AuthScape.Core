using Authscape.Reporting.Models.ReportContent;

namespace Authscape.Reporting.Models
{
    public class WidgetItem
    {
        public WidgetItem(string name = "")
        {
            Name = name;
        }

        public string Name { get; set; }
        public BaseReportContent Content { get; set; }

        // Grid positioning (12-column grid system)
        public int Row { get; set; } = 0;
        public int Column { get; set; } = 0;
        public int ColumnSpan { get; set; } = 12; // Default full width
        public int RowSpan { get; set; } = 1;

        // Optional: Unique identifier for client-side tracking
        public string? WidgetId { get; set; }
    }
}
