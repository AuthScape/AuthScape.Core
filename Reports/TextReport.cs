using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("D4E5F6A7-B8C9-0D1E-2F3A-4B5C6D7E8F90")]
    public class TextReport : FullReportEntity, IFullReport
    {
        public TextReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                widgets.Add(new WidgetItem("Sample Text Report")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new TextReportContent()
                    {
                        Title = "Summary",
                        Text = "This is a sample text report widget. You can use this to display any text content such as summaries, notes, or descriptions alongside your charts in a dashboard."
                    }
                });

                return widgets;
            });
        }
    }
}
