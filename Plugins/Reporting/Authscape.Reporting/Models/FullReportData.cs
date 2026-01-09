namespace Authscape.Reporting.Models
{
    public class FullReportData
    {
        public bool IsFullReport { get; set; } = true;
        public List<WidgetData> Widgets { get; set; } = new List<WidgetData>();
    }
}
