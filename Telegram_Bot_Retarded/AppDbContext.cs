using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot.Types;
using Telegram_Bot_Retarded;

namespace TelegramBotDemo
{
    public class AppDbContext : DbContext
    {
        public DbSet<BotUser> Users { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Строка подключения к LocalDB (встроенный MSSQL в Visual Studio)
            optionsBuilder.UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=TelegramBotDb;Trusted_Connection=True;");
        }
    }
}