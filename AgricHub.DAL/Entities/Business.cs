using AgricHub.DAL.Entities.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Entities
{ 
    public class Business
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string BusinessName { get; set; }
        public string Description { get; set; }
        public bool IsVerified { get; set; } = false;
        public string Address { get; set; }
        public string? ImagePath { get; set; }
        public int ConsultantId { get; set; }
        public DateTime DateCreated { get; set; } = new DateTime();

        // Navigation properties
        public virtual Consultant Consultant { get; set; }
        public virtual ICollection<Service> Services { get; set; } = new List<Service>();
    }
}