// Controllers/AdminController.cs
using LuceneApi.Models; // For IndexStats model
using LuceneApi.Services; // For ISearchService
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;

namespace LuceneApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Base route: /api/admin
    public class AdminController : ControllerBase
    {
        private readonly ISearchService _searchService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ISearchService searchService, ILogger<AdminController> logger)
        {
            _searchService = searchService;
            _logger = logger;
        }

        // POST /api/admin/optimize
        /// <summary>
        /// Triggers optimization (segment merging) of the Lucene index.
        /// This can be a long-running and I/O intensive operation.
        /// </summary>
        /// <returns>Status message indicating success or failure.</returns>
        [HttpPost("optimize")]
        [ProducesResponseType(typeof(string), 200)] // OK with a status message
        [ProducesResponseType(500)] // Internal Server Error
        public IActionResult OptimizeIndex()
        {
            try
            {
                _logger.LogInformation("Received admin request to optimize index.");
                _searchService.OptimizeIndex(); // Call the service method
                _logger.LogInformation("Index optimization request processed by service.");
                // Return a simple confirmation message
                return Ok("Index optimization process initiated successfully.");
            }
            catch (InvalidOperationException ex)
            {
                // Catch specific errors from the service if needed
                _logger.LogError(ex, "Service operation error during index optimization.");
                return StatusCode(500, $"An internal configuration error occurred: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Catch generic errors
                _logger.LogError(ex, "Unexpected error during index optimization request.");
                return StatusCode(500, "An error occurred during the index optimization process.");
            }
        }

        // GET /api/admin/stats
        /// <summary>
        /// Retrieves basic statistics about the current state of the Lucene index.
        /// </summary>
        /// <returns>An IndexStats object or an error response.</returns>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(IndexStats), 200)] // OK with stats object
        [ProducesResponseType(500)] // Internal Server Error
        public ActionResult<IndexStats> GetIndexStatistics()
        {
            try
            {
                _logger.LogDebug("Received admin request for index stats.");
                var stats = _searchService.GetIndexStats(); // Call the service method
                _logger.LogDebug("Returning index stats.");
                // Return the stats object
                return Ok(stats);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Service operation error while getting index stats.");
                return StatusCode(500, $"An internal configuration error occurred: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving index statistics.");
                // Return a status code indicating error
                return StatusCode(500, "An error occurred while retrieving index statistics.");
            }
        }
    }
}