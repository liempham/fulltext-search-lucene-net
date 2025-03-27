// Controllers/MessagesController.cs
using LuceneApi.Models; // DTOs, Message model
using LuceneApi.Services; // ISearchService
using Microsoft.AspNetCore.Mvc; // Controller base classes and attributes
using Microsoft.Extensions.Logging; // Logging
using System;

namespace LuceneApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Base route: /api/messages
    public class MessagesController : ControllerBase
    {
        private readonly ISearchService _searchService;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(ISearchService searchService, ILogger<MessagesController> logger)
        {
            _searchService = searchService;
            _logger = logger;
        }

        // POST /api/messages
        /// <summary>
        /// Adds a new message to the system and the search index.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(Message), 200)] // OK with created object
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)] // Bad request on validation failure
        [ProducesResponseType(500)] // Internal server error
        public IActionResult AddMessage([FromBody] AddMessageDto newMessageDto)
        {
            // ModelState validation happens automatically via [ApiController]
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState); // Return structured validation errors
            }

            // Create the full Message domain object
            var message = new Message
            {
                Id = Guid.NewGuid().ToString(), // Generate unique ID server-side
                Sender = newMessageDto.Sender,
                Recipients = newMessageDto.Recipients, // Assumes model binding works
                Subject = newMessageDto.Subject,
                Body = newMessageDto.Body,
                Timestamp = DateTime.UtcNow // Assign timestamp server-side
            };

            try
            {
                _searchService.AddMessageToIndex(message);
                _logger.LogInformation("Successfully added message with ID {MessageId}", message.Id);
                // Return the newly created message object, including generated ID and timestamp
                return Ok(message);
            }
            catch (InvalidOperationException ex)
            { // Catch specific exceptions if needed
                _logger.LogError(ex, "Service operation error while adding message: {@Dto}", newMessageDto);
                return StatusCode(500, $"An internal configuration error occurred: {ex.Message}");
            }
            catch (Exception ex) // Catch broader exceptions
            {
                _logger.LogError(ex, "Generic error occurred while adding message from DTO {@Dto}", newMessageDto);
                return StatusCode(500, "An internal server error occurred while adding the message.");
            }
        }

        // PUT /api/messages/{id}
        /// <summary>
        /// Updates an existing message identified by its ID.
        /// </summary>
        /// <param name="id">The unique ID of the message to update.</param>
        /// <param name="updateDto">The new data for the message.</param>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(Message), 200)] // OK with updated object
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)] // Bad request
        // Optional: ProducesResponseType(404) // Not Found (if you check existence first)
        [ProducesResponseType(500)] // Internal server error
        public IActionResult UpdateMessage(string id, [FromBody] UpdateMessageDto updateDto)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("Message ID in URL path cannot be empty.");
            }
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            // Create the full Message object with the updated data
            // Use the ID from the route parameter, ignore any ID potentially in the DTO
            var updatedMessageData = new Message
            {
                Id = id, // Use ID from route
                Sender = updateDto.Sender,
                Recipients = updateDto.Recipients,
                Subject = updateDto.Subject,
                Body = updateDto.Body,
                Timestamp = DateTime.UtcNow // Update timestamp on modification
            };

            try
            {
                // UpdateDocument in Lucene handles add-if-not-exists, so a 404 check isn't strictly needed
                // unless your business logic requires preventing creation via PUT.
                _searchService.UpdateMessageInIndex(id, updatedMessageData);
                _logger.LogInformation("Successfully updated message ID {MessageId}", id);
                // Return the updated data
                return Ok(updatedMessageData);
            }
            // Optional: Catch a custom NotFoundException if your service layer implements checks
            // catch (NotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Service operation error while updating message ID {MessageId}: {@Dto}", id, updateDto);
                return StatusCode(500, $"An internal configuration error occurred: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating message ID {MessageId}: {@Dto}", id, updateDto);
                return StatusCode(500, "An internal server error occurred while updating the message.");
            }
        }

        // DELETE /api/messages/{id}
        /// <summary>
        /// Deletes a message identified by its ID from the index.
        /// </summary>
        /// <param name="id">The unique ID of the message to delete.</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)] // No Content on successful deletion
        [ProducesResponseType(400)] // Bad Request (e.g., empty ID)
        // Optional: ProducesResponseType(404) // Not Found (delete is often idempotent, so maybe not needed)
        [ProducesResponseType(500)] // Internal server error
        public IActionResult DeleteMessage(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("Message ID in URL path cannot be empty.");
            }

            try
            {
                _searchService.DeleteMessageFromIndex(id);
                _logger.LogInformation("Successfully deleted message ID {MessageId}", id);
                // HTTP 204 No Content is standard for successful DELETE with no body response
                return NoContent();
            }
            // Optional: Catch NotFoundException if service layer checks existence
            // catch (NotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Service operation error while deleting message ID {MessageId}", id);
                return StatusCode(500, $"An internal configuration error occurred: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting message ID {MessageId}", id);
                return StatusCode(500, "An internal server error occurred while deleting the message.");
            }
        }
    }
}