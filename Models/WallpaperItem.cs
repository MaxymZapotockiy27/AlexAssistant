using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlexAssistant.Models
{
    public class WallpaperItem
    {
        public string Name { get; set; }
        public string UniqueId { get; set; }
        public WallpaperFileType FileType { get; set; }
        public string Path { get; set; }
        public bool IsResource { get; set; }
    }
}
