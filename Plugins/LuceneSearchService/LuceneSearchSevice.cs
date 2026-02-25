using AuthScape.LuceneSearch.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Lucene.Net.Util;
using Microsoft.Extensions.Options;
using Services.Database;

namespace AuthScape.LuceneSearch
{
    public interface ILuceneSearchSevice
    {
        void CreateIndex(List<LuceneDocument> documents, string? storagePath = null);
        Task<SearchResults> Search(string input, string field, int totalResults = 10);
        Task<SearchResults> Search(string input, string[] field, int totalResults = 10);
    }

    public class LuceneSearchSevice : ILuceneSearchSevice
    {
        readonly AppSettings appSettings;
        readonly LuceneVersion luceneVersion;
        public LuceneSearchSevice(IOptions<AppSettings> appSettings)
        {
            this.appSettings = appSettings.Value;
            luceneVersion = LuceneVersion.LUCENE_48;
        }

        public void CreateIndex(List<LuceneDocument> documents, string? storagePath = null)
        {
            AzureDirectory azureDirectory = new AzureDirectory(appSettings.LuceneSearch.StorageConnectionString, appSettings.LuceneSearch.Container);

            //Create an analyzer to process the text
            Analyzer standardAnalyzer = new StandardAnalyzer(luceneVersion);

            //Create an index writer
            IndexWriterConfig indexConfig = new IndexWriterConfig(luceneVersion, standardAnalyzer);
            indexConfig.OpenMode = OpenMode.CREATE;
            IndexWriter writer = new IndexWriter(azureDirectory, indexConfig);


            foreach (var document in documents)
            {
                Lucene.Net.Documents.Document doc = new Lucene.Net.Documents.Document();

                var fields = document.GetFields();
                foreach (var field in fields)
                {
                    switch (field.FieldType)
                    {
                        case Models.FieldType.DescriptionOrBody:
                            doc.Add(new TextField(field.Name, (string)field.Value, field.StoreField ? Field.Store.YES : Field.Store.NO));
                            break;
                        case Models.FieldType.StringField:
                            doc.Add(new StringField(field.Name, (string)field.Value, field.StoreField ? Field.Store.YES : Field.Store.NO));
                            break;
                        case Models.FieldType.NumericDocValuesField:
                            doc.Add(new NumericDocValuesField(field.Name, (long)field.Value));
                            break;
                        case Models.FieldType.DoubleField:
                            doc.Add(new DoubleField(field.Name, (double)field.Value, field.StoreField ? Field.Store.YES : Field.Store.NO));
                            break;
                        case Models.FieldType.BinaryDocValuesField:
                            doc.Add(new BinaryDocValuesField(field.Name, (BytesRef)field.Value));
                            break;
                        case Models.FieldType.SortedDocValuesField:
                            doc.Add(new SortedDocValuesField(field.Name, (BytesRef)field.Value));
                            break;
                        case Models.FieldType.SortedSetDocValuesField:
                            doc.Add(new SortedSetDocValuesField(field.Name, (BytesRef)field.Value));
                            break;
                        case Models.FieldType.Int32Field:
                            doc.Add(new Int32Field(field.Name, (int)field.Value, field.StoreField ? Field.Store.YES : Field.Store.NO));
                            break;
                        case Models.FieldType.Int64Field:
                            doc.Add(new Int64Field(field.Name, (long)field.Value, field.StoreField ? Field.Store.YES : Field.Store.NO));
                            break;
                        case Models.FieldType.SingleField:
                            doc.Add(new SingleField(field.Name, (float)field.Value, field.StoreField ? Field.Store.YES : Field.Store.NO));
                            break;
                    }
                }

                writer.AddDocument(doc);
            }

            //Flush and commit the index data to the directory
            writer.Flush(false, false);
            writer.Commit();
            writer.Dispose();

            // Clear local cache so next search downloads the fresh index
            ClearCache();
        }

        private string GetCachePath()
        {
            return Path.Combine(
                Environment.GetEnvironmentVariable("HOME") ?? Path.GetTempPath(),
                "cache", "LuceneCache", appSettings.LuceneSearch.Container
            );
        }

        /// <summary>
        /// Downloads all Lucene index blobs from Azure Blob Storage to a local directory.
        /// Skips download if a .download_complete marker exists.
        /// </summary>
        private async Task EnsureLocalIndexCache()
        {
            string cachePath = GetCachePath();
            var completeMarker = Path.Combine(cachePath, ".download_complete");

            if (File.Exists(completeMarker))
            {
                return;
            }

            // Clean up any partial download
            if (System.IO.Directory.Exists(cachePath))
            {
                foreach (var file in System.IO.Directory.GetFiles(cachePath))
                    File.Delete(file);
            }

            System.IO.Directory.CreateDirectory(cachePath);

            string containerLocation = appSettings.LuceneSearch.Container;
            var firstSlash = containerLocation.IndexOf('/');
            string containerName;
            string blobPrefix;

            if (firstSlash < 0)
            {
                containerName = containerLocation;
                blobPrefix = "";
            }
            else
            {
                containerName = containerLocation.Substring(0, firstSlash);
                blobPrefix = containerLocation.Substring(firstSlash + 1);
                if (!blobPrefix.EndsWith("/"))
                    blobPrefix += "/";
            }

            var blobServiceClient = new BlobServiceClient(appSettings.LuceneSearch.StorageConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            var downloadedFiles = new List<string>();
            try
            {
                await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: string.IsNullOrEmpty(blobPrefix) ? null : blobPrefix))
                {
                    var blobFileName = blobItem.Name;
                    if (!string.IsNullOrEmpty(blobPrefix) && blobFileName.StartsWith(blobPrefix))
                    {
                        blobFileName = blobFileName.Substring(blobPrefix.Length);
                    }

                    if (string.IsNullOrWhiteSpace(blobFileName) || blobFileName.Contains("/"))
                        continue;

                    var localFilePath = Path.Combine(cachePath, blobFileName);
                    BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);

                    using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
                    {
                        await blobClient.DownloadToAsync(fileStream);
                    }
                    downloadedFiles.Add(localFilePath);
                }
            }
            catch
            {
                foreach (var file in downloadedFiles)
                {
                    try { File.Delete(file); } catch { }
                }
                throw;
            }

            if (downloadedFiles.Count > 0)
            {
                File.WriteAllText(completeMarker, "");
            }
        }

        public void ClearCache()
        {
            string cachePath = GetCachePath();
            if (System.IO.Directory.Exists(cachePath))
            {
                foreach (string file in System.IO.Directory.GetFiles(cachePath))
                    File.Delete(file);
            }
        }

        public async Task<SearchResults> Search(string input, string field, int totalResults = 10)
        {
            var results = new SearchResults();

            await EnsureLocalIndexCache();

            string cachePath = GetCachePath();
            using var localDirectory = new SimpleFSDirectory(new DirectoryInfo(cachePath));

            if (!DirectoryReader.IndexExists(localDirectory))
            {
                return results;
            }

            IndexSearcher searcher = new IndexSearcher(DirectoryReader.Open(localDirectory));

            QueryParser parser = new QueryParser(luceneVersion, field, new StandardAnalyzer(luceneVersion));

            Query query = parser.Parse(input);
            TopDocs topDocs = searcher.Search(query, n: totalResults);

            foreach (var doc in topDocs.ScoreDocs)
            {
                Lucene.Net.Documents.Document resultDoc = searcher.Doc(doc.Doc);
                results.Documents.Add(new SearchResultDocument(resultDoc, doc.Score, doc.ShardIndex));
            }

            results.TotalResults = topDocs.TotalHits;
            results.Searcher = searcher;

            return results;
        }

        public async Task<SearchResults> Search(string input, string[] field, int totalResults = 10)
        {
            var results = new SearchResults();

            await EnsureLocalIndexCache();

            string cachePath = GetCachePath();
            using var localDirectory = new SimpleFSDirectory(new DirectoryInfo(cachePath));

            if (!DirectoryReader.IndexExists(localDirectory))
            {
                return results;
            }

            IndexSearcher searcher = new IndexSearcher(DirectoryReader.Open(localDirectory));

            MultiFieldQueryParser parser = new MultiFieldQueryParser(luceneVersion, field, new StandardAnalyzer(luceneVersion));

            Query query = parser.Parse(input);
            TopDocs topDocs = searcher.Search(query, n: totalResults);

            foreach (var doc in topDocs.ScoreDocs)
            {
                Lucene.Net.Documents.Document resultDoc = searcher.Doc(doc.Doc);
                results.Documents.Add(new SearchResultDocument(resultDoc, doc.Score, doc.ShardIndex));
            }

            results.TotalResults = topDocs.TotalHits;
            results.Searcher = searcher;

            return results;
        }
    }
}
