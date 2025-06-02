using AuthScape.Document.Mapping.Models;

namespace Services
{
    public interface IFileMappingService
    {
        Task OnRowExecute(dynamic instance, DocumentComponent documentComponent);
        Task OnCompleted(List<dynamic> objects, DocumentComponent documentComponent);
    }

    public class FileMappingService : IFileMappingService
    {
        public async Task OnRowExecute(dynamic instance, DocumentComponent documentComponent)
        {

        }

        public async Task OnCompleted(List<dynamic> objects, DocumentComponent documentComponent)
        {

        }
    }
}
