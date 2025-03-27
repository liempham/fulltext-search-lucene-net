// Services/ISearchService.cs
using LuceneApi.Models;
using System;
using System.Collections.Generic;

namespace LuceneApi.Services
{

    /// <summary>
    /// Interface for Lucene search and indexing operations.
    /// </summary>
    public interface ISearchService : IDisposable // Ensure IDisposable is inherited
    {
        /// <summary>
        /// Builds or rebuilds the Lucene index from a collection of messages.
        /// </summary>
        void BuildIndex(IEnumerable<Message> messages, bool recreateIndex = true);

        /// <summary>
        /// Searches the Lucene index based on a query string.
        /// </summary>
        SearchResult Search(string queryString, int maxResults = 10);

        /// <summary>
        /// Adds a single message to the existing Lucene index.
        /// </summary>
        void AddMessageToIndex(Message message);

        /// <summary>
        /// Updates an existing message in the index using its unique ID.
        /// </summary>
        void UpdateMessageInIndex(string messageId, Message updatedMessageData); // New

        /// <summary>
        /// Deletes a message from the index using its unique ID.
        /// </summary>
        void DeleteMessageFromIndex(string messageId); // New

        /// <summary>
        /// Optimizes the index by merging segments. Can be I/O intensive.
        /// </summary>
        void OptimizeIndex(); // New

        /// <summary>
        /// Retrieves basic statistics about the current state of the index.
        /// </summary>
        IndexStats GetIndexStats(); // New
    }

}