﻿namespace AuthScape.Document.Models
{
    public class IdFromDomainResponse
    {
        public long? CompanyId { get; set; }
        public long? DemoCompanyId { get; set; }
        public string CompanyName { get; set; }
        public string FavIcon { get; set; }
        public string? GoogleAnalytics4Code { get; set; }
        public string? MicrosoftClarityCode { get; set; }
        public string? HubspotTrackingCode { get; set; }
        public string? CanonicalBaseUrl { get; set; }
        public bool RedirectTrafficToCanonical { get; set; }
    }
}