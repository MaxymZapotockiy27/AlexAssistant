using AlexAssistant.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Speech.Synthesis;

namespace AlexAssistant.Services
{

    public class ReminderService
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentBag<RemindersModel> _reminders = new();
        private static readonly TimeSpan _reminderGracePeriod = TimeSpan.FromMinutes(5);

        public void AddReminder(RemindersModel reminder)
        {
            _reminders.Add(reminder);
        }

        public void Start()
        {
            Task.Run(() => MonitorReminders(_cts.Token));
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        private async Task MonitorReminders(CancellationToken token)
        {
            Console.WriteLine("[ReminderService] Monitoring started.");
            try
            {
               
                await Task.Delay(TimeSpan.FromSeconds(3), token);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("[ReminderService] Monitoring cancelled during initial delay.");
                return;
            }

            while (!token.IsCancellationRequested)
            {
                var now = DateTime.Now;
  
                var remindersToCheck = _reminders.Where(r => !r.IsCompleted).ToList();

                foreach (var reminder in remindersToCheck)
                {
        
                    if (IsTimeToTrigger(reminder, now))
                    {
                   
                        reminder.IsCompleted = true;
                        Console.WriteLine($"[ReminderService] Marking '{reminder.TitleTex}' as completed. Triggering execution.");

             
                        _ = Task.Run(() => ExecuteReminder(reminder), CancellationToken.None);

                     
                    }
                }

                try
                {
                    
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
                catch (TaskCanceledException)
                {
                 
                    break; 
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ReminderService] Error in monitoring loop: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                }
            }
            Console.WriteLine("[ReminderService] Monitoring stopped.");
        }

        private bool IsTimeToTrigger(RemindersModel reminder, DateTime now)
        {
            DateTime? targetDateTime = GetNextReminderDateTime(reminder);

            if (!targetDateTime.HasValue)
            {
                Console.WriteLine($"[ReminderService] Warning: Could not determine target time for reminder '{reminder.TitleTex}'. Skipping.");
  
                return false;
            }

     
            bool isWithinGracePeriod = targetDateTime.Value <= now && targetDateTime.Value > now.Subtract(_reminderGracePeriod);

          

            return isWithinGracePeriod;
       
        }

        private DateTime? GetNextReminderDateTime(RemindersModel reminder)
        {
            if (TimeSpan.TryParse(reminder.TimeTex, out var timePart))
            {
                var datePart = reminder.ScheduledDate.Date;
                return datePart.Add(timePart);
            }
            return null;
        }

        private async Task ExecuteReminder(RemindersModel reminder) 
        {
            Console.WriteLine($"[ReminderService] Executing reminder: Title='{reminder.TitleTex}', Task='{reminder.TaskTex}'");
            try
            {
                using var synthesizer = new SpeechSynthesizer();
                synthesizer.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
                synthesizer.Rate = 0;

                string reminderText = $"Reminder! {reminder.TitleTex}." +
                              (reminder.IsImportant ? "This is an important task!" : "");

                synthesizer.Speak(reminderText);

                Console.WriteLine($"[ReminderService] Reminder '{reminder.TitleTex}' spoken successfully.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReminderService] Error executing reminder '{reminder.TitleTex}': {ex.Message}");
              
            }
        }

    }

}

