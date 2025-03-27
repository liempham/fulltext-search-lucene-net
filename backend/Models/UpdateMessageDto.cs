// Models/UpdateMessageDto.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LuceneApi.Models
{
    /// <summary>
    /// DTO for updating an existing message. ID is typically provided via URL route.
    /// </summary>
    public class UpdateMessageDto
    {
        [Required(ErrorMessage = "Sender is required.")]
        public required string Sender { get; set; }

        [Required(ErrorMessage = "Recipients list is required.")]
        [MinLength(1, ErrorMessage = "At least one recipient is required.")]
        public required List<string> Recipients { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        public required string Subject { get; set; }

        [Required(ErrorMessage = "Body is required.")]
        public required string Body { get; set; }

        // Timestamp is usually updated server-side during the update action if needed
        // Or could be passed optionally from client if client manages it
    }
}