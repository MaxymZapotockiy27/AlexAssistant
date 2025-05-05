using AlexAssistant.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlexAssistant.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<WorldCity> WorldCities { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Data Source=DESKTOP-993SLA0;Initial Catalog=AlexAssistant;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False");
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<WorldCity>().HasNoKey();
            modelBuilder.Entity<WorldCity>().ToTable("WorldCities");
        }
    }
}
