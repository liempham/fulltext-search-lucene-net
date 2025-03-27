// Controllers/SearchController.cs
using LuceneApi.Models;
using LuceneApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations; // For Required attribute

namespace LuceneApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(ISearchService searchService, ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    [HttpGet] // GET /api/search?q=your_query
    public ActionResult<SearchResult> SearchMessages([FromQuery, Required] string q, [FromQuery] int limit = 10)
    {
        if (limit <= 0) limit = 10; // Ensure positive limit
        if (limit > 100) limit = 100; // Set a reasonable max limit

        _logger.LogInformation("Received search request for query: '{Query}' with limit: {Limit}", q, limit);

        try
        {
            var result = _searchService.Search(q, limit);
            // Add logging based on result
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                _logger.LogWarning("Search for '{Query}' resulted in error: {Error}", q, result.ErrorMessage);
                // Optionally return a specific HTTP status code for errors, e.g., BadRequest
                // return BadRequest(result);
            }
            else
            {
                _logger.LogInformation("Search for '{Query}' returned {HitCount} hits.", q, result.Hits.Count);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during search for query: {Query}", q);
            return StatusCode(500, new SearchResult { Hits = new List<MessageHit>(), TotalHits = 0, ErrorMessage = "An internal server error occurred." });
        }
    }

    // --- Optional: Endpoint to trigger re-indexing ---
    // Use with caution, maybe add authorization later
    [HttpPost("reindex")] // POST /api/search/reindex
    public IActionResult RebuildIndex()
    {
        _logger.LogInformation("Received request to rebuild index.");
        try
        {
            // In a real app, load data from your actual data source here
            var sampleData = SearchService.GetSampleData();
            _searchService.BuildIndex(sampleData, recreateIndex: true);
            _logger.LogInformation("Index rebuild completed successfully.");
            return Ok("Index rebuild initiated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild index.");
            return StatusCode(500, "Failed to rebuild index.");
        }
    }
}