using AlexAssistant.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace AlexAssistant.Services
{
    public class UserService
    {
        private string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AlexAssistant",
            "userData.json");
        public void SaveUserData(UserData userData)
        {
            try
            {
                                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(userData, Formatting.Indented);
                File.WriteAllText(filePath, json);
                Properties.Settings.Default.AssistantEnabled = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving user data: {ex.Message}");
            }
        }

        public bool UserDataExist()
        {
            return File.Exists(filePath);
        }
    }
}
