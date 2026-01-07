using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    public class SankeyChartReport : ReportEntity, IReport
    {
        public SankeyChartReport() : base() { }

        public override async Task<Widget> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                // Sample: Website traffic flow
                var dataPoints = new List<SankeyDataPoint>
                {
                    // Source channels to landing pages
                    new SankeyDataPoint { From = "Organic Search", To = "Homepage", Weight = 5000 },
                    new SankeyDataPoint { From = "Organic Search", To = "Product Page", Weight = 3000 },
                    new SankeyDataPoint { From = "Paid Ads", To = "Landing Page", Weight = 4000 },
                    new SankeyDataPoint { From = "Paid Ads", To = "Product Page", Weight = 2000 },
                    new SankeyDataPoint { From = "Social Media", To = "Homepage", Weight = 2500 },
                    new SankeyDataPoint { From = "Social Media", To = "Blog", Weight = 1500 },
                    new SankeyDataPoint { From = "Email Campaign", To = "Landing Page", Weight = 3000 },
                    new SankeyDataPoint { From = "Direct", To = "Homepage", Weight = 4000 },

                    // Pages to actions
                    new SankeyDataPoint { From = "Homepage", To = "Product View", Weight = 6000 },
                    new SankeyDataPoint { From = "Homepage", To = "Exit", Weight = 5500 },
                    new SankeyDataPoint { From = "Landing Page", To = "Sign Up", Weight = 4500 },
                    new SankeyDataPoint { From = "Landing Page", To = "Exit", Weight = 2500 },
                    new SankeyDataPoint { From = "Product Page", To = "Add to Cart", Weight = 3500 },
                    new SankeyDataPoint { From = "Product Page", To = "Exit", Weight = 1500 },
                    new SankeyDataPoint { From = "Blog", To = "Product View", Weight = 800 },
                    new SankeyDataPoint { From = "Blog", To = "Exit", Weight = 700 },

                    // Actions to conversions
                    new SankeyDataPoint { From = "Product View", To = "Add to Cart", Weight = 4000 },
                    new SankeyDataPoint { From = "Product View", To = "Exit", Weight = 2800 },
                    new SankeyDataPoint { From = "Add to Cart", To = "Purchase", Weight = 5000 },
                    new SankeyDataPoint { From = "Add to Cart", To = "Abandoned", Weight = 2500 },
                    new SankeyDataPoint { From = "Sign Up", To = "Purchase", Weight = 2000 },
                    new SankeyDataPoint { From = "Sign Up", To = "No Action", Weight = 2500 }
                };

                return new Widget("Website Traffic Flow")
                {
                    Content = new SankeyChartContent()
                    {
                        DataPoints = dataPoints,
                        Title = "Website User Flow Analysis"
                    },
                };
            });
        }
    }
}
