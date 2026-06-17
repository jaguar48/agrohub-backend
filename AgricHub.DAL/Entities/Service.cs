using System;
using System.Collections.Generic;
namespace AgricHub.DAL.Entities
{
    public class Service
    {
        public int Id { get; set; }
        public string ServiceName { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; } = true;
        public int CategoryId { get; set; }
        public Category Category { get; set; }
        public string? ImagePath { get; set; }
        public string? MediaJson { get; set; }  // ← JSON array of additional media paths
        public int BusinessId { get; set; }
        public virtual Business Business { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public virtual ICollection<ServicePackage> Packages { get; set; } = new List<ServicePackage>();
    }
}