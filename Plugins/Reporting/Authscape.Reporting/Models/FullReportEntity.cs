using Authscape.Models.Reporting.Attributes;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Authscape.Reporting.Models
{
    public class FullReportEntity
    {
        public List<WidgetItem> Widgets { get; set; } = new List<WidgetItem>();
        public DbContext[] Databases { get; set; }

        public Guid Id
        {
            get
            {
                return Guid.Parse(GetType().GetCustomAttribute<ReportNameAttribute>().ReportId);
            }
        }

        public virtual async Task<List<WidgetItem>> OnRequest(string payLoad)
        {
            throw new Exception("Report must override OnRequest method to populate data.");
        }
    }
}
