using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using AlexAssistant.Models;
namespace AlexAssistant.Models
{
    public class UserData
    {
        public string UserName { get; set; }
        public string UserCity { get; set; }
        public string UserCountry { get; set; }
        public DateTime? UserBirthday { get; set; }

    }
    
}
