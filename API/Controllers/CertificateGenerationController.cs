using CsvHelper.Configuration.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using AuthScape.Models.Certificates;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Wordprocessing;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CertificateGenerationController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get(string applicationName)
        {
            var inMemoryCertificates = new List<InMemoryCertificate>();
            var encryptionCert = applicationName + "-encryptionCert";
            var signingCert = applicationName + "-signingCert";

            inMemoryCertificates.Add(new InMemoryCertificate()
            {
                CertificateName = encryptionCert,
                ExpiryYear = 2
            });

            inMemoryCertificates.Add(new InMemoryCertificate()
            {
                CertificateName = signingCert,
                ExpiryYear = 2
            });

            foreach (var inMemoryCertificate in inMemoryCertificates)
            {
                using var algorithm = RSA.Create(keySizeInBits: 2048);

                var subject = new X500DistinguishedName("CN=" + inMemoryCertificate.CertificateName);
                var request = new CertificateRequest(subject, algorithm, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, critical: true));

                var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(inMemoryCertificate.ExpiryYear));
                var pfxCertificate = certificate.Export(X509ContentType.Pfx, string.Empty);

                inMemoryCertificate.CertificateData = pfxCertificate;
            }

            // Create an in-memory zip file
            byte[] zipFileBytes;
            using (var zipStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var inMemoryCertificate in inMemoryCertificates)
                    {
                        var zipEntry = archive.CreateEntry(inMemoryCertificate.CertificateName + ".pfx", CompressionLevel.Fastest);
                        using (var entryStream = zipEntry.Open())
                        using (var certificateStream = new MemoryStream(inMemoryCertificate.CertificateData))
                        {
                            certificateStream.CopyTo(entryStream);
                        }
                    }
                }
                zipFileBytes = zipStream.ToArray();
            }

            return File(zipFileBytes, "application/zip", "Certificates.zip");
        }
    }
}
