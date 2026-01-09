using Microsoft.EntityFrameworkCore;

namespace Authscape.Reporting.Models
{
    public interface IFullReport
    {
        Guid Id { get; }
        List<WidgetItem> Widgets { get; set; }
        Task<List<WidgetItem>> OnRequest(string payLoad);
        DbContext[] Databases { get; set; }
    }
}
