using AlexAssistant.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AlexAssistant.Data
{
    public static class ReminderStorage
    {
        public static List<RemindersModel> LoadReminders(string path)
        {
            if (!File.Exists(path))
            {
                return new List<RemindersModel>();
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<RemindersModel>>(json) ?? new List<RemindersModel>();
        }
    }
}
