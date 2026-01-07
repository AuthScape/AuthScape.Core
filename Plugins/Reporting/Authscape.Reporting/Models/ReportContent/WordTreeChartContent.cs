namespace Authscape.Reporting.Models.ReportContent
{
    public class WordTreeChartContent : BaseReportContent
    {
        /// <summary>
        /// List of phrases for implicit word tree (simple mode)
        /// Each phrase is a sentence that will be parsed into words
        /// </summary>
        public List<WordTreePhrase> Phrases { get; set; }

        /// <summary>
        /// The word to use as the root of the tree (optional)
        /// If not set, Google Charts will determine the root automatically
        /// </summary>
        public string RootWord { get; set; }

        /// <summary>
        /// Word tree format: "implicit" (phrases) or "explicit" (nodes)
        /// Default is "implicit"
        /// </summary>
        public string Format { get; set; } = "implicit";
    }

    public class WordTreePhrase
    {
        /// <summary>
        /// The phrase text (e.g., "cats are better than dogs")
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Optional size/weight of this phrase
        /// </summary>
        public int? Size { get; set; }
    }
}
