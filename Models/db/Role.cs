using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordEventBot.Models.db
{
    public class Role
    {
        public int Id { get; set; }
        public GuildConfig Guild { get; set; }
        public string Name { get; set; }
        public string Template { get; set; }
        public string Emote { get; set; }
        public string Filter { get; set; }
        public int LeadTime { get; set; } = 15;
    }
}
