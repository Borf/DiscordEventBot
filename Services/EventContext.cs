using DiscordEventBot.Models.db;
using FileContextCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordEventBot.Services
{
    public class EventContext : DbContext
    {
        public DbSet<GuildConfig> Guilds { get; set; }
        public DbSet<DiscordBot> DiscordBots { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseFileContextDatabase()
                                .UseLoggerFactory(LoggerFactory.Create(builder => { builder.AddConsole(); }))
                                .EnableSensitiveDataLogging()
                                .EnableDetailedErrors();
            ;
        }

        public DbSet<DiscordEventBot.Models.db.Role> Role { get; set; }


    }
}
