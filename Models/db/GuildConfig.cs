using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordEventBot.Models.db
{
    public class GuildConfig
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public string EventChannelName { get; set; }
        public string CalendarUrl { get; set; }

        public string Template { get; set; }

        public List<Role> Roles { get; set; } = new List<Role>();
        public DiscordBot DiscordBot { get; set; }
    }
}
