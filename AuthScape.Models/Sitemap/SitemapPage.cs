using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AuthScape.Models.Sitemap
{
    public class SitemapResponse
    {
        public List<SitemapPage> Pages { get; set; }
    }

    public class SitemapPage
    {
        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("changefreq")]
        public string Changefreq { get; set; } = "weekly"; // daily, weekly, monthly, yearly

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = "0.7"; // 0.0 to 1.0
    }
}
