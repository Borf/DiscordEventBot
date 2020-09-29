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
        public string Name { get; set; }    //the internal name in discord
        public string DiscordText { get; set; }    //the text at the end of the discord post
        public string Template { get; set; }    //the template for when people get notified
        public string Emote { get; set; } //the emote to use
        public string Filter { get; set; } //the filter to use on the events (location=xxx, etc)
        public int LeadTime { get; set; } = 15; //how many minutes in advance to ping
    }
}
