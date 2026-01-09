using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-888888888888")]
    public class TreeMapChartReport : FullReportEntity, IFullReport
    {
        public TreeMapChartReport() : base() { }

        public override async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var widgets = new List<WidgetItem>();

                var nodes = new List<TreeMapDataPoint>
                {
                    // Root
                    new TreeMapDataPoint { Id = "Company", Parent = null, Value = 0, ColorValue = 0 },

                    // Departments
                    new TreeMapDataPoint { Id = "Engineering", Parent = "Company", Value = 0, ColorValue = 0 },
                    new TreeMapDataPoint { Id = "Sales", Parent = "Company", Value = 0, ColorValue = 0 },
                    new TreeMapDataPoint { Id = "Marketing", Parent = "Company", Value = 0, ColorValue = 0 },
                    new TreeMapDataPoint { Id = "Operations", Parent = "Company", Value = 0, ColorValue = 0 },

                    // Engineering sub-departments
                    new TreeMapDataPoint { Id = "Frontend", Parent = "Engineering", Value = 450000, ColorValue = 15 },
                    new TreeMapDataPoint { Id = "Backend", Parent = "Engineering", Value = 520000, ColorValue = 18 },
                    new TreeMapDataPoint { Id = "DevOps", Parent = "Engineering", Value = 280000, ColorValue = 8 },
                    new TreeMapDataPoint { Id = "QA", Parent = "Engineering", Value = 180000, ColorValue = 5 },

                    // Sales sub-departments
                    new TreeMapDataPoint { Id = "Enterprise", Parent = "Sales", Value = 380000, ColorValue = 22 },
                    new TreeMapDataPoint { Id = "SMB", Parent = "Sales", Value = 220000, ColorValue = 12 },
                    new TreeMapDataPoint { Id = "Channel", Parent = "Sales", Value = 150000, ColorValue = 6 },

                    // Marketing sub-departments
                    new TreeMapDataPoint { Id = "Digital", Parent = "Marketing", Value = 200000, ColorValue = 10 },
                    new TreeMapDataPoint { Id = "Content", Parent = "Marketing", Value = 120000, ColorValue = 4 },
                    new TreeMapDataPoint { Id = "Events", Parent = "Marketing", Value = 80000, ColorValue = 2 },

                    // Operations
                    new TreeMapDataPoint { Id = "HR", Parent = "Operations", Value = 150000, ColorValue = 3 },
                    new TreeMapDataPoint { Id = "Finance", Parent = "Operations", Value = 180000, ColorValue = 4 },
                    new TreeMapDataPoint { Id = "Legal", Parent = "Operations", Value = 100000, ColorValue = 2 }
                };

                widgets.Add(new WidgetItem("Department Budget Allocation")
                {
                    Row = 0,
                    Column = 0,
                    ColumnSpan = 12,
                    Content = new TreeMapChartContent()
                    {
                        Title = "Budget by Department",
                        Nodes = nodes,
                        ShowScale = true,
                        MaxDepth = 2,
                        HeaderHeight = 20
                    }
                });

                return widgets;
            });
        }
    }
}
