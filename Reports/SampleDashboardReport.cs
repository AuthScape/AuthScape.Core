using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    public class SampleDashboardReport : FullReportEntity, IFullReport
    {
        public SampleDashboardReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                // Row 0: Two pie charts side by side
                widgets.Add(new WidgetItem("Sales by Region")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 6,
                    Content = new PieChartContent
                    {
                        Title = "Sales by Region",
                        DataPoints = new List<PieChartDataPoint>
                        {
                            new PieChartDataPoint { Name = "North", Number = 35 },
                            new PieChartDataPoint { Name = "South", Number = 25 },
                            new PieChartDataPoint { Name = "East", Number = 20 },
                            new PieChartDataPoint { Name = "West", Number = 20 }
                        }
                    }
                });

                widgets.Add(new WidgetItem("Revenue by Product")
                {
                    Row = 0,
                    Column = 6,
                    ColumnSpan = 6,
                    Content = new PieChartContent
                    {
                        Title = "Revenue by Product",
                        DataPoints = new List<PieChartDataPoint>
                        {
                            new PieChartDataPoint { Name = "Product A", Number = 40 },
                            new PieChartDataPoint { Name = "Product B", Number = 30 },
                            new PieChartDataPoint { Name = "Product C", Number = 30 }
                        }
                    }
                });

                // Row 1: Full-width bar chart
                widgets.Add(new WidgetItem("Monthly Sales Trends")
                {
                    Row = 1,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new BarChartContent
                    {
                        XAxis = new List<string> { "Month", "Jan", "Feb", "Mar", "Apr", "May", "Jun" },
                        dataPoints = new List<BarChartDataPoint>
                        {
                            new BarChartDataPoint
                            {
                                Label = "Revenue",
                                Data = new List<double> { 12000, 15000, 13500, 18000, 16500, 21000 }
                            },
                            new BarChartDataPoint
                            {
                                Label = "Expenses",
                                Data = new List<double> { 8000, 9500, 8500, 11000, 10000, 12500 }
                            }
                        }
                    }
                });

                // Row 2: Area chart and gauge
                widgets.Add(new WidgetItem("User Growth")
                {
                    Row = 2,
                    Column = 0,
                    ColumnSpan = 8,
                    Content = new AreaChartContent
                    {
                        XAxis = new List<string> { "Quarter", "Q1", "Q2", "Q3", "Q4" },
                        dataPoints = new List<AreaChartDataPoint>
                        {
                            new AreaChartDataPoint
                            {
                                Label = "Active Users",
                                Data = new List<double> { 1500, 2200, 3100, 4500 }
                            },
                            new AreaChartDataPoint
                            {
                                Label = "New Signups",
                                Data = new List<double> { 500, 700, 900, 1400 }
                            }
                        }
                    }
                });

                widgets.Add(new WidgetItem("Performance Score")
                {
                    Row = 2,
                    Column = 8,
                    ColumnSpan = 4,
                    Content = new GaugeChartContent
                    {
                        DataPoints = new List<GaugeDataPoint>
                        {
                            new GaugeDataPoint { Label = "CPU", value = 68 },
                            new GaugeDataPoint { Label = "Memory", value = 75 },
                            new GaugeDataPoint { Label = "Disk", value = 45 }
                        }
                    }
                });

                return widgets;
            });
        }
    }
}
