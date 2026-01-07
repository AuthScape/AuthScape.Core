using Authscape.Models.Reporting.Attributes;
using Authscape.Reporting.Models;
using Authscape.Reporting.Models.ReportContent;

namespace Reports
{
    [ReportName("F1A2B3C4-D5E6-7890-ABCD-444444444444")]
    public class GanttChartReport : ReportEntity, IReport
    {
        public GanttChartReport() : base() { }

        public override async Task<Widget> OnRequest(string payLoad)
        {
            return await Task.Run(() =>
            {
                var startDate = DateTime.Now.Date;

                var tasks = new List<GanttDataPoint>
                {
                    new GanttDataPoint
                    {
                        TaskId = "Research",
                        TaskName = "Requirements Gathering",
                        Resource = "Planning",
                        StartDate = startDate,
                        EndDate = startDate.AddDays(5),
                        PercentComplete = 100,
                        Dependencies = null
                    },
                    new GanttDataPoint
                    {
                        TaskId = "Design",
                        TaskName = "System Design",
                        Resource = "Planning",
                        StartDate = startDate.AddDays(5),
                        EndDate = startDate.AddDays(12),
                        PercentComplete = 80,
                        Dependencies = "Research"
                    },
                    new GanttDataPoint
                    {
                        TaskId = "Backend",
                        TaskName = "Backend Development",
                        Resource = "Development",
                        StartDate = startDate.AddDays(12),
                        EndDate = startDate.AddDays(30),
                        PercentComplete = 45,
                        Dependencies = "Design"
                    },
                    new GanttDataPoint
                    {
                        TaskId = "Frontend",
                        TaskName = "Frontend Development",
                        Resource = "Development",
                        StartDate = startDate.AddDays(15),
                        EndDate = startDate.AddDays(35),
                        PercentComplete = 30,
                        Dependencies = "Design"
                    },
                    new GanttDataPoint
                    {
                        TaskId = "Testing",
                        TaskName = "Integration Testing",
                        Resource = "QA",
                        StartDate = startDate.AddDays(30),
                        EndDate = startDate.AddDays(40),
                        PercentComplete = 0,
                        Dependencies = "Backend,Frontend"
                    },
                    new GanttDataPoint
                    {
                        TaskId = "Deploy",
                        TaskName = "Deployment",
                        Resource = "Operations",
                        StartDate = startDate.AddDays(40),
                        EndDate = startDate.AddDays(45),
                        PercentComplete = 0,
                        Dependencies = "Testing"
                    }
                };

                return new Widget("Project Timeline")
                {
                    Content = new GanttChartContent()
                    {
                        Title = "Project Development Timeline",
                        Tasks = tasks,
                        ShowCriticalPath = true,
                        TrackHeight = 30
                    },
                };
            });
        }
    }
}
