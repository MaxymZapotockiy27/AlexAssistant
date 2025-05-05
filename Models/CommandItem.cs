using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlexAssistant.Models
{
    public class CommandItem
    {
        public string Phrase { get; set; } = string.Empty;
        public CommandActionType ActionType { get; set; }
        public string ActionTarget { get; set; } = string.Empty; 
    }
}
