using AlexAssistant.Data;
using AlexAssistant.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Shapes;

namespace AlexAssistant
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IConfiguration Configuration { get; private set; }
        public static ReminderService ReminderService { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            Configuration = builder.Build();

            Task.Run(() =>
            {
                //using (var context = new ApplicationDbContext())
                //{
                //    var _ = context.WorldCities.FirstOrDefault();
                //}
            });
            ReminderService = new ReminderService();
            var reminderspath = System.IO.Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
           "AlexAssistant",
           "reminders.json");
            var reminders = ReminderStorage.LoadReminders(reminderspath);
            foreach (var reminder in reminders)
            {
                ReminderService.AddReminder(reminder);
            }

            ReminderService.Start();
            base.OnStartup(e); 
        }
        protected override void OnExit(ExitEventArgs e)
        {
            ReminderService?.Stop();
            base.OnExit(e);
        }
    }

}
