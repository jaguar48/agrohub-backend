using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities.Models
{
    /// <summary>
    /// Stores all platform configuration as key-value pairs.
    /// Admins can update these at runtime without touching code or config files.
    /// </summary>
    public class PlatformSetting
    {
        public int Id { get; set; }
        public string Key { get; set; }   // e.g. "platform.name"
        public string Value { get; set; }   // e.g. "AgricHub"
        public string Category { get; set; }   // "general" | "email" | "financials" | "booking" | "integrations" | "features"
        public string? Label { get; set; }   // Human-readable label for the admin UI
        public string? InputType { get; set; }   // "text" | "password" | "number" | "toggle" | "select"
        public bool IsSecret { get; set; }   // Mask value in API response (e.g. API keys)
        public string? Group { get; set; }   // Sub-group within category
        public int SortOrder { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

