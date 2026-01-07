using Authscape.Models.Reporting;
using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;
using Authscape.Reporting.Models.Timeline;
using AuthScape.Models.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Database;
using System.Reflection;

namespace Authscape.Reporting.Services
{
    public interface IReportService
    {
        Task<IReport> RunReport(AppDomain domain, Guid id, string payLoad, DbContext[] Databases);
    }

    public class ReportService : IReportService
    {
        readonly AppSettings appSettings;
        public ReportService(IOptions<AppSettings> appSettings)
        {
            this.appSettings = appSettings.Value;
        }

        public async Task<IReport> RunReport(AppDomain domain, Guid id, string payLoad, DbContext[] Databases)
        {
            var report = GetReport(domain, id);
            var instance = (IReport)Activator.CreateInstance(report.ReportType);

            instance.Databases = Databases;
            instance.RawData = await instance.OnRequest(payLoad);






            if (instance.RawData.Content.GetType() == typeof(AreaChartContent))
            {
                var areaCharts = (AreaChartContent)instance.RawData.Content;

                // get the column data
                var columnData = new List<string>();
                columnData.Add(areaCharts.XAxis.FirstOrDefault());
                foreach (var dataPoint in areaCharts.dataPoints)
                {
                    columnData.Add(dataPoint.Label);
                }
                instance.Columns = columnData;


                // get the values for the columns
                var list = new List<List<object>>();

                int columnIndex = 0;
                foreach (var columnDat in areaCharts.XAxis.Skip(1))
                {
                    var data = new List<object>();
                    data.Add(columnDat);

                    foreach (var dp in areaCharts.dataPoints)
                    {
                        data.Add(dp.Data[columnIndex]);
                    }

                    list.Add(data);

                    columnIndex++;
                }

                instance.Data = list;
                instance.ReportType = ReportType.AreaChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(BarChartContent))
            {
                var areaCharts = (BarChartContent)instance.RawData.Content;

                // get the column data
                var columnData = new List<string>();
                columnData.Add(areaCharts.XAxis.FirstOrDefault());
                foreach (var dataPoint in areaCharts.dataPoints)
                {
                    columnData.Add(dataPoint.Label);
                }
                instance.Columns = columnData;


                // get the values for the columns
                var list = new List<List<object>>();

                int columnIndex = 0;
                foreach (var columnDat in areaCharts.XAxis.Skip(1))
                {
                    var data = new List<object>();
                    data.Add(columnDat);

                    foreach (var dp in areaCharts.dataPoints)
                    {
                        data.Add(dp.Data[columnIndex]);
                    }

                    list.Add(data);

                    columnIndex++;
                }

                instance.Data = list;
                instance.ReportType = ReportType.BarChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(BubbleChartContent))
            {
                var areaCharts = (BubbleChartContent)instance.RawData.Content;

                // get the column data
                var columnData = new List<string>();


                // maybe change this to an array that is adjustable so the dev can add more fields
                columnData.Add("ID");
                columnData.Add(areaCharts.HorizontalText);
                columnData.Add(areaCharts.VerticalText);
                columnData.Add(areaCharts.Name);

                instance.Columns = columnData;


                // get the values for the columns
                var list = new List<object>();

                foreach (var columnDat in areaCharts.dataPoints)
                {
                    var data = new List<object>();

                    data.Add(columnDat.Id);
                    data.Add(columnDat.X);
                    data.Add(columnDat.Y);
                    data.Add(columnDat.Size);

                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.BubbleChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(BarCandleStickContent))
            {
                var areaCharts = (BarCandleStickContent)instance.RawData.Content;

                // get the column data
                var columnData = new List<string>();
                columnData.Add(areaCharts.XAxis.FirstOrDefault());
                foreach (var dataPoint in areaCharts.dataPoints)
                {
                    columnData.Add(dataPoint.Label);
                }
                instance.Columns = columnData;


                // get the values for the columns
                var list = new List<List<object>>();

                int columnIndex = 0;
                foreach (var columnDat in areaCharts.XAxis.Skip(1))
                {
                    var data = new List<object>();
                    data.Add(columnDat);

                    foreach (var dp in areaCharts.dataPoints)
                    {
                        data.Add(dp.Data[columnIndex]);
                    }

                    list.Add(data);

                    columnIndex++;
                }

                instance.Data = list;
                instance.ReportType = ReportType.CandleStickChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(GaugeChartContent))
            {
                var areaCharts = (GaugeChartContent)instance.RawData.Content;

                // get the column data
                var columnData = new List<string>();
                columnData.Add("Label");
                columnData.Add("Value");
                instance.Columns = columnData;


                // get the values for the columns
                var list = new List<object>();

                foreach (var columnDat in areaCharts.DataPoints)
                {
                    var data = new List<object>();
                    data.Add(columnDat.Label);
                    data.Add(columnDat.value);
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.GaugeChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(PieChartContent))
            {
                var areaCharts = (PieChartContent)instance.RawData.Content;

                // get the column data
                var columnData = new List<string>();
                columnData.Add("Name");
                columnData.Add("Number");
                instance.Columns = columnData;


                // get the values for the columns
                var list = new List<object>();

                foreach (var columnDat in areaCharts.DataPoints)
                {
                    var data = new List<object>();
                    data.Add(columnDat.Name);
                    data.Add(columnDat.Number);
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.PieChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(HistogramsContent))
            {
                var areaCharts = (HistogramsContent)instance.RawData.Content;

                // get the column data
                var columnData = new List<string>();
                columnData.Add("Name");
                columnData.Add("Number");
                instance.Columns = columnData;


                // get the values for the columns
                var list = new List<object>();

                foreach (var columnDat in areaCharts.DataPoints)
                {
                    var data = new List<object>();
                    data.Add(columnDat.Name);
                    data.Add(columnDat.Number);
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.HistogramsReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(TimelineChartContent))
            {
                var areaCharts = (TimelineChartContent)instance.RawData.Content;

                // get the column data
                var columnData = new List<TimelineHeader>();

                columnData.Add(new TimelineHeader("string", "timeline"));
                columnData.Add(new TimelineHeader("date", "Start"));
                columnData.Add(new TimelineHeader("date", "End"));

                instance.Columns = columnData;


                // get the values for the columns
                var list = new List<object>();

                foreach (var columnDat in areaCharts.DataPoints)
                {
                    var data = new List<object>();
                    data.Add(columnDat.Label);
                    data.Add(columnDat.StartDate.ToString("DT-yyyy-MM-dd"));
                    data.Add(columnDat.EndDate.ToString("DT-yyyy-MM-dd"));
                    list.Add(data);
                }

                instance.Data = columnData;
                instance.Data = list;
                instance.ReportType = ReportType.TimelineReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(WordTreeChartContent))
            {
                var wordTree = (WordTreeChartContent)instance.RawData.Content;

                // WordTree uses simple phrase-based format
                // Column: ["Phrases"] or ["Phrases", "Size"] if sizes are provided
                var columnData = new List<string>();
                columnData.Add("Phrases");

                bool hasSize = wordTree.Phrases?.Any(p => p.Size.HasValue) ?? false;
                if (hasSize)
                {
                    columnData.Add("Size");
                }
                instance.Columns = columnData;

                // Data: each row is a phrase (and optionally size)
                var list = new List<object>();
                if (wordTree.Phrases != null)
                {
                    foreach (var phrase in wordTree.Phrases)
                    {
                        if (hasSize)
                        {
                            var data = new List<object>();
                            data.Add(phrase.Text);
                            data.Add(phrase.Size ?? 1);
                            list.Add(data);
                        }
                        else
                        {
                            var data = new List<object>();
                            data.Add(phrase.Text);
                            list.Add(data);
                        }
                    }
                }

                instance.Data = list;
                instance.ReportType = ReportType.WordTree;

                // Set WordTree-specific options
                instance.Options = new Dictionary<string, object>
                {
                    { "wordtree", new Dictionary<string, object>
                        {
                            { "format", wordTree.Format ?? "implicit" },
                            { "word", wordTree.RootWord ?? "" }
                        }
                    }
                };
            }
            else if (instance.RawData.Content.GetType() == typeof(SankeyChartContent))
            {
                var sankey = (SankeyChartContent)instance.RawData.Content;

                // Sankey requires: From, To, Weight columns
                var columnData = new List<string>();
                columnData.Add("From");
                columnData.Add("To");
                columnData.Add("Weight");
                instance.Columns = columnData;

                // Data: each row is [from, to, weight]
                var list = new List<object>();
                if (sankey.DataPoints != null)
                {
                    foreach (var point in sankey.DataPoints)
                    {
                        var data = new List<object>();
                        data.Add(point.From);
                        data.Add(point.To);
                        data.Add(point.Weight);
                        list.Add(data);
                    }
                }

                instance.Data = list;
                instance.ReportType = ReportType.SanKey;
            }
            else if (instance.RawData.Content.GetType() == typeof(LineChartContent))
            {
                var lineChart = (LineChartContent)instance.RawData.Content;

                var columnData = new List<string>();
                columnData.Add(lineChart.XAxis.FirstOrDefault());
                foreach (var dataPoint in lineChart.DataPoints)
                {
                    columnData.Add(dataPoint.Label);
                }
                instance.Columns = columnData;

                var list = new List<List<object>>();
                int columnIndex = 0;
                foreach (var columnDat in lineChart.XAxis.Skip(1))
                {
                    var data = new List<object>();
                    data.Add(columnDat);
                    foreach (var dp in lineChart.DataPoints)
                    {
                        data.Add(dp.Data[columnIndex]);
                    }
                    list.Add(data);
                    columnIndex++;
                }

                instance.Data = list;
                instance.ReportType = ReportType.LineChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(ComboChartContent))
            {
                var comboChart = (ComboChartContent)instance.RawData.Content;

                var columnData = new List<string>();
                columnData.Add(comboChart.XAxis.FirstOrDefault());
                foreach (var dataPoint in comboChart.DataPoints)
                {
                    columnData.Add(dataPoint.Label);
                }
                instance.Columns = columnData;

                var list = new List<List<object>>();
                int columnIndex = 0;
                foreach (var columnDat in comboChart.XAxis.Skip(1))
                {
                    var data = new List<object>();
                    data.Add(columnDat);
                    foreach (var dp in comboChart.DataPoints)
                    {
                        data.Add(dp.Data[columnIndex]);
                    }
                    list.Add(data);
                    columnIndex++;
                }

                instance.Data = list;
                instance.ReportType = ReportType.ComboChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(DonutChartContent))
            {
                var donutChart = (DonutChartContent)instance.RawData.Content;

                var columnData = new List<string>();
                columnData.Add("Name");
                columnData.Add("Value");
                instance.Columns = columnData;

                var list = new List<object>();
                foreach (var dataPoint in donutChart.DataPoints)
                {
                    var data = new List<object>();
                    data.Add(dataPoint.Name);
                    data.Add(dataPoint.Value);
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.DonutChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(GanttChartContent))
            {
                var ganttChart = (GanttChartContent)instance.RawData.Content;

                // Gantt chart requires typed columns
                var columnData = new List<TimelineHeader>();
                columnData.Add(new TimelineHeader("string", "Task ID"));
                columnData.Add(new TimelineHeader("string", "Task Name"));
                columnData.Add(new TimelineHeader("string", "Resource"));
                columnData.Add(new TimelineHeader("date", "Start Date"));
                columnData.Add(new TimelineHeader("date", "End Date"));
                columnData.Add(new TimelineHeader("number", "Duration"));
                columnData.Add(new TimelineHeader("number", "Percent Complete"));
                columnData.Add(new TimelineHeader("string", "Dependencies"));
                instance.Columns = columnData;

                var list = new List<object>();
                foreach (var task in ganttChart.Tasks)
                {
                    var data = new List<object>();
                    data.Add(task.TaskId);
                    data.Add(task.TaskName);
                    data.Add(task.Resource ?? "");
                    data.Add(task.StartDate.ToString("DT-yyyy-MM-dd"));
                    data.Add(task.EndDate.ToString("DT-yyyy-MM-dd"));
                    data.Add(task.Duration);
                    data.Add(task.PercentComplete);
                    data.Add(task.Dependencies ?? "");
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.GanttChartReport;

                // Set Gantt-specific options
                instance.Options = new Dictionary<string, object>
                {
                    { "gantt", new Dictionary<string, object>
                        {
                            { "trackHeight", ganttChart.TrackHeight },
                            { "criticalPathEnabled", ganttChart.ShowCriticalPath },
                            { "criticalPathStyle", new Dictionary<string, object>
                                {
                                    { "stroke", "#FA896B" },
                                    { "strokeWidth", 2 }
                                }
                            }
                        }
                    }
                };
            }
            else if (instance.RawData.Content.GetType() == typeof(GeoChartContent))
            {
                var geoChart = (GeoChartContent)instance.RawData.Content;

                var columnData = new List<string>();
                columnData.Add("Location");
                columnData.Add(geoChart.ValueLabel ?? "Value");
                instance.Columns = columnData;

                var list = new List<object>();
                foreach (var dataPoint in geoChart.DataPoints)
                {
                    var data = new List<object>();
                    data.Add(dataPoint.Location);
                    data.Add(dataPoint.Value);
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.GeoChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(OrgChartContent))
            {
                var orgChart = (OrgChartContent)instance.RawData.Content;

                var columnData = new List<string>();
                columnData.Add("Name");
                columnData.Add("Parent");
                columnData.Add("Tooltip");
                instance.Columns = columnData;

                var list = new List<object>();
                foreach (var node in orgChart.Nodes)
                {
                    var data = new List<object>();
                    data.Add(node.Name);
                    data.Add(string.IsNullOrEmpty(node.ParentId) ? "" : orgChart.Nodes.FirstOrDefault(n => n.Id == node.ParentId)?.Name ?? "");
                    data.Add(node.Tooltip ?? "");
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.OrgChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(SteppedAreaChartContent))
            {
                var steppedAreaChart = (SteppedAreaChartContent)instance.RawData.Content;

                var columnData = new List<string>();
                columnData.Add(steppedAreaChart.XAxis.FirstOrDefault());
                foreach (var dataPoint in steppedAreaChart.DataPoints)
                {
                    columnData.Add(dataPoint.Label);
                }
                instance.Columns = columnData;

                var list = new List<List<object>>();
                int columnIndex = 0;
                foreach (var columnDat in steppedAreaChart.XAxis.Skip(1))
                {
                    var data = new List<object>();
                    data.Add(columnDat);
                    foreach (var dp in steppedAreaChart.DataPoints)
                    {
                        data.Add(dp.Data[columnIndex]);
                    }
                    list.Add(data);
                    columnIndex++;
                }

                instance.Data = list;
                instance.ReportType = ReportType.SteppedAreaChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(TreeMapChartContent))
            {
                var treeMapChart = (TreeMapChartContent)instance.RawData.Content;

                var columnData = new List<string>();
                columnData.Add("ID");
                columnData.Add("Parent");
                columnData.Add("Value");
                columnData.Add("Color");
                instance.Columns = columnData;

                var list = new List<object>();
                foreach (var node in treeMapChart.Nodes)
                {
                    var data = new List<object>();
                    data.Add(node.Id);
                    data.Add(node.Parent ?? "");
                    data.Add(node.Value);
                    data.Add(node.ColorValue ?? node.Value);
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.TreeMapChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(ScatterChartContent))
            {
                var scatterChart = (ScatterChartContent)instance.RawData.Content;

                var columnData = new List<string>();
                columnData.Add(scatterChart.XName ?? "X");
                columnData.Add(scatterChart.YName ?? "Y");
                instance.Columns = columnData;

                var list = new List<object>();
                foreach (var dataPoint in scatterChart.DataPoints)
                {
                    var data = new List<object>();
                    data.Add(dataPoint.X);
                    data.Add(dataPoint.Y);
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.ScatterChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(CalendarChartContent))
            {
                var calendarChart = (CalendarChartContent)instance.RawData.Content;

                var columnData = new List<TimelineHeader>();
                columnData.Add(new TimelineHeader("date", "Date"));
                columnData.Add(new TimelineHeader("number", "Value"));
                instance.Columns = columnData;

                var list = new List<object>();
                foreach (var dataPoint in calendarChart.DataPoints)
                {
                    var data = new List<object>();
                    data.Add(dataPoint.Date.ToString("DT-yyyy-MM-dd"));
                    data.Add(dataPoint.Size);
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.CalendarChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(ColumnChartContent))
            {
                var columnChart = (ColumnChartContent)instance.RawData.Content;

                var columnData = new List<string>();
                columnData.Add(columnChart.XAxis.FirstOrDefault());
                foreach (var dataPoint in columnChart.DataPoints)
                {
                    columnData.Add(dataPoint.Label);
                }
                instance.Columns = columnData;

                var list = new List<List<object>>();
                int columnIndex = 0;
                foreach (var columnDat in columnChart.XAxis.Skip(1))
                {
                    var data = new List<object>();
                    data.Add(columnDat);
                    foreach (var dp in columnChart.DataPoints)
                    {
                        data.Add(dp.Data[columnIndex]);
                    }
                    list.Add(data);
                    columnIndex++;
                }

                instance.Data = list;
                instance.ReportType = ReportType.ColumnChartReport;
            }
            else if (instance.RawData.Content.GetType() == typeof(WaterfallChartContent))
            {
                var waterfallChart = (WaterfallChartContent)instance.RawData.Content;

                // Waterfall uses candlestick-like format
                var columnData = new List<string>();
                columnData.Add("Category");
                columnData.Add("Bottom");
                columnData.Add("Start");
                columnData.Add("End");
                columnData.Add("Top");
                instance.Columns = columnData;

                var list = new List<object>();
                decimal runningTotal = 0;

                foreach (var dataPoint in waterfallChart.DataPoints)
                {
                    var data = new List<object>();
                    data.Add(dataPoint.Label);

                    if (dataPoint.IsTotal)
                    {
                        data.Add(0);
                        data.Add(0);
                        data.Add(runningTotal);
                        data.Add(runningTotal);
                    }
                    else
                    {
                        decimal prevTotal = runningTotal;
                        runningTotal += dataPoint.Value;

                        if (dataPoint.Value >= 0)
                        {
                            data.Add(prevTotal);
                            data.Add(prevTotal);
                            data.Add(runningTotal);
                            data.Add(runningTotal);
                        }
                        else
                        {
                            data.Add(runningTotal);
                            data.Add(runningTotal);
                            data.Add(prevTotal);
                            data.Add(prevTotal);
                        }
                    }
                    list.Add(data);
                }

                instance.Data = list;
                instance.ReportType = ReportType.WaterfallChartReport;
            }


            return instance;
        }

        private List<string> GetColumnNames(IEnumerable<object> Objs)
        {
            var columns = new List<string>();
            if (Objs != null)
            {
                var first = Objs.FirstOrDefault();
                if (first != null)
                {
                    foreach (var prop in first.GetType().GetProperties())
                    {
                        columns.Add(prop.Name);
                    }
                }
            }
            return columns;
        }

        private List<List<object>> GetValues(IEnumerable<object> Objs)
        {
            var data = new List<List<object>>();
            if (Objs != null)
            {
                foreach (var obj in Objs)
                {
                    var data2 = new List<object>();
                    foreach (var prop in obj.GetType().GetProperties())
                    {
                        var test = prop.GetValue(obj);
                        data2.Add(test);
                    }
                    data.Add(data2);
                }
            }
            return data;
        }


        public Report GetReport(AppDomain domain, Guid id)
        {
            return GetReports(domain).Where(r => r.Id == id).FirstOrDefault();
        }

        public IList<Report> GetReports(AppDomain domain, long? UserId = null)
        {
            var reports = new List<Report>();

            var assemblies = domain.GetAssemblies();

            var reportTypes = GetReportTypesInNamespace(assemblies.Where(a => a.FullName.Contains("Reports")));

            foreach (var report in reportTypes)
            {
                var reportAttribute = report.GetCustomAttribute<ReportNameAttribute>();
                var Id = Guid.Parse(reportAttribute.ReportId);

                reports.Add(new Report()
                {
                    Id = Id,
                    ReportType = report,
                });
            }
            return reports.ToList();
        }


        private Type[] GetReportTypesInNamespace(IEnumerable<Assembly> assemblies)
        {
            var reports = assemblies.SelectMany(s => s.GetTypes())
                             .Where(c => typeof(IReport).IsAssignableFrom(c) && c.IsClass)
                             .ToArray();

            return reports;
        }
    }
}
