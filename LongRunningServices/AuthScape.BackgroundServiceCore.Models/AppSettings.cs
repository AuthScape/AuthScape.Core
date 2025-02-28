using AuthScape.Models;

namespace AuthScape.BackgroundServiceCore.Models
{
    public class AppSettings
    {
        public string Name { get; set; }
        public Stage Stage { get; set; }
        public string DatabaseContext { get; set; }
    }
}