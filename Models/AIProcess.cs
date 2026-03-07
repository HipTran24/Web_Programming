using System.ComponentModel.DataAnnotations;

namespace Wed_Project.Models
{
    public class AIProcess
    {
        [Key]
        public int ProcessId { get; set; }

        public int ContentId { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string KeyPoints { get; set; } = string.Empty;

        public double ProcessingTime { get; set; }

        public DateTime CreatedAt { get; set; }

        public Content Content { get; set; } = null!;
    }
}
