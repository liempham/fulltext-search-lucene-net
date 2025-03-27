// Models/AddMessageDto.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LuceneApi.Models;

public class AddMessageDto
{
    [Required]
    public required string Sender { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one recipient is required.")]
    public required List<string> Recipients { get; set; } // Receive as a list directly

    [Required]
    public required string Subject { get; set; }

    [Required]
    public required string Body { get; set; }
}