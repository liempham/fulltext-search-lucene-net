// Services/SearchService.cs
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store; // <--- Added for FSDirectory, AlreadyClosedException, LockObtainFailedException etc.
using Lucene.Net.Util;
using LuceneApi.Models;
using Microsoft.Extensions.Configuration; // Required for IConfiguration
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace LuceneApi.Services
{
    // --- Service Implementation ---
    public class SearchService : ISearchService
    {
        private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
        private readonly string _indexPath;
        private readonly StandardAnalyzer _analyzer;
        private readonly FSDirectory _directory;
        private readonly ILogger<SearchService> _logger;
        private IndexWriter? _writer = null;
        private readonly object _indexWriterLock = new object();

        private static readonly Dictionary<string, string> _canonicalFieldNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "sender", "Sender" }, { "recipient", "Recipient" }, { "subject", "Subject" },
            { "body", "Body" }, { "id", "Id" }
        };

        public SearchService(ILogger<SearchService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _analyzer = new StandardAnalyzer(AppLuceneVersion);
            _indexPath = configuration["Lucene:IndexPath"] ?? Path.Combine(Environment.CurrentDirectory, "lucene_index");
            _logger.LogInformation("Using Lucene Index Path: {IndexPath}", _indexPath);

            if (!System.IO.Directory.Exists(_indexPath))
            {
                try { System.IO.Directory.CreateDirectory(_indexPath); _logger.LogInformation("Created index directory: {Path}", _indexPath); }
                catch (Exception ex) { _logger.LogCritical(ex, "Failed to create index directory: {Path}", _indexPath); throw; }
            }

            try
            {
                _directory = FSDirectory.Open(_indexPath);
                if (IndexWriter.IsLocked(_directory))
                {
                    _logger.LogWarning("Index lock file found on startup. Releasing lock...");
                    IndexWriter.Unlock(_directory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to open/check index directory at {Path}. Service cannot function.", _indexPath);
                throw; // Critical failure if directory can't be opened/unlocked
            }

            InitializeWriter();
            EnsureIndexExists();
        }

        private void InitializeWriter()
        {
            if (_writer != null) return;
            lock (_indexWriterLock)
            {
                if (_writer == null)
                {
                    try
                    {
                        if (IndexWriter.IsLocked(_directory)) { IndexWriter.Unlock(_directory); } // Re-check lock
                        var indexConfig = new IndexWriterConfig(AppLuceneVersion, _analyzer) { OpenMode = OpenMode.CREATE_OR_APPEND };
                        _writer = new IndexWriter(_directory, indexConfig);
                        _logger.LogInformation("IndexWriter initialized.");
                    }
                    catch (LockObtainFailedException lockEx)
                    {
                        _logger.LogCritical(lockEx, "Failed to obtain index lock: {Path}", _indexPath); throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical(ex, "Failed to initialize IndexWriter."); throw;
                    }
                }
            }
        }

        private void EnsureIndexExists()
        {
            try
            {
                if (!DirectoryReader.IndexExists(_directory))
                {
                    _logger.LogInformation("Index not found. Building initial index...");
                    BuildIndex(GetSampleData(), recreateIndex: true);
                }
                else
                {
                    var stats = GetIndexStats(); // Log stats on startup
                    _logger.LogInformation("Existing index found. Stats: LiveDocs={NumDocs}, TotalDocs={MaxDocs}, Segments={NumSegments}", stats.NumDocs, stats.MaxDocs, stats.NumSegments);
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error during EnsureIndexExists."); }
        }

        private Document ConvertMessageToDocument(Message message)
        {
            var doc = new Document {
                 new StringField("Id", message.Id, Field.Store.YES),
                 new TextField("Sender", message.Sender, Field.Store.YES),
                 new TextField("Subject", message.Subject, Field.Store.YES),
                 new TextField("Body", message.Body, Field.Store.YES), // Stored for Edit PoC
                 new Int64Field("TimestampTicks", message.Timestamp.Ticks, Field.Store.YES),
                 new NumericDocValuesField("TimestampTicks", message.Timestamp.Ticks)
             };
            message.Recipients.ForEach(r => doc.Add(new TextField("Recipient", r, Field.Store.YES)));
            return doc;
        }

        public void AddMessageToIndex(Message message)
        {
            lock (_indexWriterLock)
            {
                InitializeWriter(); if (_writer == null) throw new InvalidOperationException("Writer not init.");
                _logger.LogInformation("Adding message ID {Id}", message.Id);
                try { _writer.AddDocument(ConvertMessageToDocument(message)); _writer.Commit(); _logger.LogInformation("Committed add {Id}", message.Id); }
                catch (Exception ex) { _logger.LogError(ex, "Error adding {Id}", message.Id); throw; }
            }
        }

        public void BuildIndex(IEnumerable<Message> messages, bool recreateIndex = true)
        {
            int count = 0;
            lock (_indexWriterLock)
            {
                InitializeWriter(); if (_writer == null) throw new InvalidOperationException("Writer not init.");
                if (recreateIndex) { _logger.LogInformation("Clearing index."); _writer.DeleteAll(); _writer.Commit(); _logger.LogInformation("Index cleared."); }
                _logger.LogInformation("Indexing documents...");
                foreach (var message in messages)
                {
                    try { _writer.AddDocument(ConvertMessageToDocument(message)); count++; }
                    catch (Exception ex) { _logger.LogError(ex, "Error indexing {Id}, skipping.", message.Id); }
                }
                _writer.Commit(); _logger.LogInformation("Finished indexing {Count} docs.", count);
            }
        }

        public void UpdateMessageInIndex(string messageId, Message updatedMessageData)
        {
            lock (_indexWriterLock)
            {
                InitializeWriter(); if (_writer == null) throw new InvalidOperationException("Writer not init.");
                _logger.LogInformation("Updating message ID {MessageId}", messageId);

                // The controller ensures updatedMessageData.Id matches messageId, no need to check/fix here.
                var term = new Term("Id", messageId); // Term identifies doc to replace
                var doc = ConvertMessageToDocument(updatedMessageData); // New doc content

                try
                {
                    _writer.UpdateDocument(term, doc); // Atomic delete-then-add
                    _writer.Commit();
                    _logger.LogInformation("Committed update for {MessageId}.", messageId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating {MessageId}", messageId); throw;
                }
            }
        }

        public void DeleteMessageFromIndex(string messageId)
        {
            lock (_indexWriterLock)
            {
                InitializeWriter(); if (_writer == null) throw new InvalidOperationException("Writer not init.");
                _logger.LogInformation("Deleting message ID {MessageId}", messageId);
                var term = new Term("Id", messageId);
                try { _writer.DeleteDocuments(term); _writer.Commit(); _logger.LogInformation("Committed delete for {MessageId}.", messageId); }
                catch (Exception ex) { _logger.LogError(ex, "Error deleting {MessageId}", messageId); throw; }
            }
        }

        public void OptimizeIndex()
        {
            lock (_indexWriterLock)
            {
                InitializeWriter(); if (_writer == null) throw new InvalidOperationException("Writer not init.");
                _logger.LogInformation("Starting index optimization (ForceMerge)...");
                try { _writer.ForceMerge(1, true); _writer.Commit(); _logger.LogInformation("Optimization complete."); }
                catch (Exception ex) { _logger.LogError(ex, "Error optimizing index."); throw; }
            }
        }

        public IndexStats GetIndexStats()
        {
            try
            {
                if (!DirectoryReader.IndexExists(_directory)) return new IndexStats(); // Empty index
                using var reader = DirectoryReader.Open(_directory);
                var numDocs = reader.NumDocs; var maxDocs = reader.MaxDoc; var numSegments = reader.Leaves.Count;
                _logger.LogDebug("Stats: NumDocs={ND}, MaxDocs={MD}, Segs={NS}", numDocs, maxDocs, numSegments);
                return new IndexStats { NumDocs = numDocs, MaxDocs = maxDocs, NumSegments = numSegments, IsOptimized = numSegments <= 1 };
            }
            catch (Exception ex) { _logger.LogError(ex, "Error getting stats."); return new IndexStats { NumDocs = -1, MaxDocs = -1, NumSegments = -1 }; }
        }

        public SearchResult Search(string queryString, int maxResults = 10)
        {
            var results = new List<MessageHit>(); long totalHits = 0; string parsedQueryStr = "";
            if (string.IsNullOrWhiteSpace(queryString)) return new SearchResult { Hits = results, TotalHits = 0, ErrorMessage = "Query required." };
            if (!DirectoryReader.IndexExists(_directory)) return new SearchResult { Hits = results, TotalHits = 0, ErrorMessage = "Index not ready." };
            string processedQueryString = PreprocessQueryString(queryString);
            try
            {
                using var reader = DirectoryReader.Open(_directory); var searcher = new IndexSearcher(reader);
                string[] searchFields = { "Sender", "Recipient", "Subject", "Body", "Id" };
                var queryParser = new MultiFieldQueryParser(AppLuceneVersion, searchFields, _analyzer) { LowercaseExpandedTerms = true };
                Query query;
                try { query = queryParser.Parse(processedQueryString); parsedQueryStr = query.ToString(); }
                catch (ParseException ex) { return new SearchResult { Hits = results, TotalHits = 0, LuceneQuery = processedQueryString, ErrorMessage = $"Invalid Query: {ex.Message}" }; }
                TopDocs topDocs = searcher.Search(query, maxResults); totalHits = topDocs.TotalHits;
                _logger.LogInformation("Search found {TotalHits} hits.", totalHits);
                foreach (var scoreDoc in topDocs.ScoreDocs)
                {
                    var doc = searcher.Doc(scoreDoc.Doc);
                    var recipients = doc.GetFields("Recipient")?.Select(f => f.GetStringValue()).ToList() ?? new List<string>();
                    long timestampTicks = doc.GetField("TimestampTicks")?.GetInt64Value() ?? 0L;
                    DateTime timestamp = timestampTicks > 0 ? new DateTime(timestampTicks, DateTimeKind.Utc) : DateTime.MinValue;
                    string body = doc.Get("Body") ?? "[Body not stored]";
                    results.Add(new MessageHit { Score = scoreDoc.Score, Message = new Message { Id = doc.Get("Id"), Sender = doc.Get("Sender"), Recipients = recipients, Subject = doc.Get("Subject"), Body = body, Timestamp = timestamp } });
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error during search for: {Query}", queryString); return new SearchResult { Hits = results, TotalHits = 0, LuceneQuery = parsedQueryStr, ErrorMessage = "Search error occurred." }; }
            return new SearchResult { Hits = results, TotalHits = totalHits, LuceneQuery = parsedQueryStr };
        }

        private string PreprocessQueryString(string queryString)
        {
            var regex = new Regex(@"(?<=^|\s)(\b[a-zA-Z_][a-zA-Z0-9_]*\b)\s*:", RegexOptions.IgnoreCase);
            var processedQuery = regex.Replace(queryString, match =>
            {
                string fieldNameOriginal = match.Groups[1].Value;
                if (_canonicalFieldNames.TryGetValue(fieldNameOriginal, out var canonicalName)) return $"{canonicalName}:";
                return match.Value;
            });
            if (processedQuery != queryString) _logger.LogDebug("Query preprocessed: '{PQ}' (from '{OQ}')", processedQuery, queryString);
            return processedQuery;
        }

        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_indexWriterLock)
                {
                    if (_writer != null)
                    {
                        _logger.LogInformation("Disposing IndexWriter...");
                        try { _writer.Dispose(); }
                        //catch (AlreadyClosedException) { _logger.LogWarning("IndexWriter was already closed during Dispose."); } // Ignore if already closed
                        catch (Exception ex) { _logger.LogError(ex, "Error disposing IndexWriter."); }
                        _writer = null;
                    }
                }
                _analyzer?.Dispose();
                _directory?.Dispose();
                _logger.LogInformation("SearchService disposed resources.");
            }
        }
        ~SearchService() { Dispose(false); }

        public static IEnumerable<Message> GetSampleData()
        {
            return new List<Message> {
                new Message { Id = "sample-001", Sender = "Alice", Recipients = new List<string> { "Bob", "Charlie" }, Subject = "Project Kickoff", Body = "Team, let's kickoff the new search project next Monday. Agenda includes setup and initial indexing strategy. Location: Mars Conf Room A.", Timestamp = DateTime.UtcNow.AddDays(-3) },
                new Message { Id = "sample-002", Sender = "Bob", Recipients = new List<string> { "Alice" }, Subject = "Re: Project Kickoff", Body = "Sounds good, Alice. I've prepared some notes on Lucene.NET configuration.", Timestamp = DateTime.UtcNow.AddDays(-2) },
                new Message { Id = "sample-003", Sender = "Charlie", Recipients = new List<string> { "Alice", "Bob", "David" }, Subject = "Frontend Setup", Body = "I've set up the basic React frontend using Vite. Ready for API integration.", Timestamp = DateTime.UtcNow.AddDays(-1) },
                new Message { Id = "sample-004", Sender = "David", Recipients = new List<string> { "Eve", "Frank" }, Subject = "API Endpoint Review", Body = "Please review the proposed API endpoints for search and add operations. Doc attached.", Timestamp = DateTime.UtcNow.AddHours(-10) },
                new Message { Id = "sample-005", Sender = "Eve", Recipients = new List<string> { "David" }, Subject = "Re: API Endpoint Review", Body = "Looks logical. Suggest adding endpoints for Update and Delete as well.", Timestamp = DateTime.UtcNow.AddHours(-5) },
                new Message { Id = "sample-006", Sender = "Frank", Recipients = new List<string> { "Alice", "Bob", "Charlie", "David", "Eve"}, Subject = "Team Lunch - Friday?", Body = "Anyone free for a team lunch this Friday to celebrate the project start? Maybe somewhere near the Mars office?", Timestamp = DateTime.UtcNow.AddHours(-2) }
            };
        }

    } // End Class
} // End Namespace