using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlexAssistant.Models
{
    public class RemindersModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TitleTex { get; set; } = string.Empty;
        public string TaskTex { get; set; } = string.Empty;
        public string TimeTex { get; set; } = string.Empty;        // Keep for time in HH:mm format
        public bool IsImportant { get; set; } = false;
        public bool IsCompleted { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.Now;  // When reminder was created
        public DateTime ScheduledDate { get; set; } = DateTime.Now; // When reminder is scheduled for
    }
}
