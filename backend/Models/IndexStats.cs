// Models/IndexStats.cs
namespace LuceneApi.Models
{
    /// <summary>
    /// Represents basic statistics for the Lucene index.
    /// </summary>
    public class IndexStats
    {
        /// <summary>
        /// Number of live (not deleted) documents in the index.
        /// </summary>
        public int NumDocs { get; set; }

        /// <summary>
        /// High-water mark of documents in the index (includes deleted ones). MaxDocs >= NumDocs.
        /// </summary>
        public int MaxDocs { get; set; }

        /// <summary>
        /// Indicates if the index is likely optimized (consists of few segments, e.g., 1).
        /// </summary>
        public bool IsOptimized { get; set; }

        /// <summary>
        /// The number of segments the index is currently composed of. Fewer is generally better for search speed.
        /// </summary>
        public int NumSegments { get; set; }
    }
}