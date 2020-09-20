using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordEventBot.Models.db
{
    public class DiscordBot
    {
        public int Id { get; set; }
        public string BotToken { get; set; }
        public string Name { get; set; }

        public List<GuildConfig> GuildConfigs { get; set; }

    }
}
