namespace Authscape.Reporting.Models.ReportContent
{
    public class TextReportContent : BaseReportContent
    {
        /// <summary>
        /// The text content to display. Supports basic HTML.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Optional title displayed above the text
        /// </summary>
        public string? Title { get; set; }
    }
}
