using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("E832DC1A-0A81-476B-982A-155900AE9F71")]
    public class WordTreeChartReport : ReportEntity, IReport
    {
        public WordTreeChartReport() : base() { }

        public override async Task<Widget> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var phrases = new List<WordTreePhrase>
                {
                    new WordTreePhrase { Text = "cats are better than dogs" },
                    new WordTreePhrase { Text = "cats eat kibble" },
                    new WordTreePhrase { Text = "cats are better than hamsters" },
                    new WordTreePhrase { Text = "cats are awesome" },
                    new WordTreePhrase { Text = "cats are people too" },
                    new WordTreePhrase { Text = "cats eat mice" },
                    new WordTreePhrase { Text = "cats meowing" },
                    new WordTreePhrase { Text = "cats in the cradle" },
                    new WordTreePhrase { Text = "cats eat mice" },
                    new WordTreePhrase { Text = "cats in the cradle lyrics" },
                    new WordTreePhrase { Text = "cats eat kibble" },
                    new WordTreePhrase { Text = "cats for adoption" },
                    new WordTreePhrase { Text = "cats are property" },
                    new WordTreePhrase { Text = "cats are evil" },
                    new WordTreePhrase { Text = "cats are weird" },
                    new WordTreePhrase { Text = "cats are mammals" },
                    new WordTreePhrase { Text = "cats are carnivores" },
                    new WordTreePhrase { Text = "cats are lazy" },
                    new WordTreePhrase { Text = "cats are the best" },
                    new WordTreePhrase { Text = "cats for sale" },
                    new WordTreePhrase { Text = "cats playing" },
                    new WordTreePhrase { Text = "cats sleeping" }
                };

                return new Widget("Word Tree - Cat Phrases")
                {
                    Content = new WordTreeChartContent()
                    {
                        Phrases = phrases,
                        RootWord = "cats",
                        Format = "implicit"
                    },
                };
            });
        }
    }
}
