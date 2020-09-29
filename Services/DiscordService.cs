using DiscordEventBot.Models.db;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordEventBot.Services
{
    public interface IDiscord
    {
        public Action<string, string> OnSendMessage { get; set; }
        public Func<string, DSharpPlus.Entities.DiscordMessage> Log { get; set; }
        public Func<string, byte[], Task> Warn { get; set; }
    }
    public class Discord : IDiscord
    {
        public Action<string, string> OnSendMessage { get; set; }
        public Func<string, DSharpPlus.Entities.DiscordMessage> Log { get; set; }
        public Func<string, byte[], Task> Warn { get; set; }
    }

    class GuildRole
    {
        public string Name { get; set; }
        public ulong Id { get; set; }
        public string Emote { get; set; }
        public string Filter { get; internal set; }
        public int LeadTime { get; internal set; }
        public string DiscordText { get; internal set; }
        public ulong MessageId { get; set; } = 0;
        public Occurrence Occurrence { get; set; } = null;
        public string Template { get; internal set; }

        public bool MatchesOccurrence(Occurrence occurrence)
        {
            if (Filter == "nolocation")
                return (occurrence.Source as CalendarEvent).Location == "";
            else if(Filter.StartsWith("location="))
                return (occurrence.Source as CalendarEvent).Location.ToLower().Contains(Filter.Substring(9).ToLower());
            return false;
        }
    }
    class GuildInfo
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; internal set; } = 0;
        public Task Task { get; internal set; }
        public string CalendarUrl { get; internal set; }
        public DiscordClient Client { get; internal set; }
        public string Template { get; internal set; }
        public List<GuildRole> Roles { get; } = new List<GuildRole>();
    }

    class DiscordInfo
    {
        public int DiscordBotId { get; set; }
        public Dictionary<ulong, GuildInfo> Guilds { get; set; } = new Dictionary<ulong, GuildInfo>();
    }

    public class DiscordService : BackgroundService
    {
        private ILogger<DiscordService> logger;
        private Dictionary<int, DiscordClient> clients = new Dictionary<int, DiscordClient>();
        private Dictionary<BaseDiscordClient, DiscordInfo> discordInfo = new Dictionary<BaseDiscordClient, DiscordInfo>();

        public DiscordService(IDiscord discordBack, ILogger<DiscordService> logger)
        {
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(true)
            {
                using (var context = new EventContext())
                {
                    context.DiscordBots.ToList().ForEach(async config =>
                    {
                        if (!clients.ContainsKey(config.Id))
                        {
                            clients[config.Id] = await StartUpDiscordClientAsync(config);
                        }
                    });
                    await Task.Delay(60000);
                }
            }
        }

        private async Task<DiscordClient> StartUpDiscordClientAsync(DiscordBot BotConfig)
        {
            logger.LogInformation("Connecting to discord server");
            DiscordClient client = new DiscordClient(new DiscordConfiguration
            {
                Token = BotConfig.BotToken,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
            });
            discordInfo[client] = new DiscordInfo()
            {
                DiscordBotId = BotConfig.Id
            };

            client.GuildAvailable += GuildAvailable;
            client.MessageReactionAdded += ReactionAdded;
            client.MessageReactionRemoved += ReactionRemoved;
            client.MessageReactionsCleared += ReactionCleared;
            await client.ConnectAsync();
            return client;
        }

        private async Task ReactionCleared(MessageReactionsClearEventArgs e)
        {
            var info = discordInfo[e.Client].Guilds[e.Channel.GuildId];

            if (e.Message.Id != info.MessageId)
                return;
            foreach (var role in info.Roles)
                foreach (var member in e.Channel.Guild.Members)
                    await e.Channel.Guild.RevokeRoleAsync(member, e.Channel.Guild.GetRole(role.Id), "User removed role clearing reactions");

            
        }

        private async Task ReactionRemoved(MessageReactionRemoveEventArgs e)
        {
            var info = discordInfo[e.Client].Guilds[e.Channel.GuildId];

            if (e.Message.Id != info.MessageId)
                return;
            foreach (var role in info.Roles)
                if (e.Emoji == DiscordEmoji.FromName(info.Client, $":{role.Emote}:"))
                    await e.Channel.Guild.RevokeRoleAsync(await e.Channel.Guild.GetMemberAsync(e.User.Id), e.Channel.Guild.GetRole(role.Id), "User removed role through reaction");
        }

        private async Task ReactionAdded(MessageReactionAddEventArgs e)
        {
            var info = discordInfo[e.Client].Guilds[e.Channel.GuildId];

            if (e.Message.Id != info.MessageId)
                return;

            bool parsed = false;
            foreach(var role in info.Roles)
            {
                if (e.Emoji == DiscordEmoji.FromName(info.Client, $":{role.Emote}:"))
                {
                    await e.Channel.Guild.GrantRoleAsync(await e.Channel.Guild.GetMemberAsync(e.User.Id), e.Channel.Guild.GetRole(role.Id));
                    parsed = true;
                }
            }
            if(!parsed)
                await e.Message.DeleteReactionAsync(e.Emoji, e.User);

        }

        private async Task GuildAvailable(GuildCreateEventArgs e)
        {
            using (var context = new EventContext())
            {
                var botId = clients.FirstOrDefault(x => x.Value == e.Client).Key;
                var bot = context.DiscordBots.Include(b => b.GuildConfigs).ThenInclude(gc => gc.Roles).FirstOrDefault(b => b.Id == botId);
                
                var config = bot.GuildConfigs.FirstOrDefault(g => g.Id == e.Guild.Id);
                if(config == null)
                {
                    config = new GuildConfig()
                    {
                        Id = e.Guild.Id,
                        DiscordBot = bot,
                        Name = e.Guild.Name,
                        CalendarUrl = "",
                        EventChannelName = "midgard-reporter"
                    };
                    context.Guilds.Add(config);
                    await context.SaveChangesAsync();
                }

                var info = new GuildInfo()
                {
                    Client = e.Client as DiscordClient,
                    Id = e.Guild.Id,
                    Name = e.Guild.Name,
                    ChannelId = e.Guild.Channels.FirstOrDefault(c => c.Name == config.EventChannelName.ToLower()).Id,
                    CalendarUrl = config.CalendarUrl,
                    Template = config.Template
                };

                foreach(var r in config.Roles)
                {
                    GuildRole gr = new GuildRole()
                    {
                        Name = r.Name,
                        DiscordText = r.DiscordText,
                        Emote = r.Emote,
                        Filter = r.Filter,
                        LeadTime = r.LeadTime,
                        Template = r.Template
                    };
                    var eventNotificationRole = e.Guild.Roles.Where(r => r.Name == gr.Name).FirstOrDefault();
                    if (eventNotificationRole == null)
                        eventNotificationRole = await e.Guild.CreateRoleAsync(gr.Name);
                    gr.Id = eventNotificationRole.Id;
                    info.Roles.Add(gr);
                }

                discordInfo[e.Client].Guilds[e.Guild.Id] = info;

                var channel = e.Guild.GetChannel(info.ChannelId);
                var messages = await channel.GetMessagesAsync();
                for(int i = 0; i < messages.Count; i++)
                {
                    if (messages[i].Author.IsCurrent)
                    {
                        if (info.MessageId == 0)
                            info.MessageId = messages[i].Id;
                        //else
                        //    await messages[i].DeleteAsync();
                    }
                }
                if (info.MessageId == 0)
                    info.MessageId = (await channel.SendMessageAsync("Bot is starting...")).Id;
                else
                    await (await channel.GetMessageAsync(info.MessageId)).ModifyAsync("Bot is starting...");

                foreach(var role in info.Roles)
                {
                    var msg = await channel.GetMessageAsync(info.MessageId);
                    if (!msg.Reactions.Any(r => r.IsMe && r.Emoji == DiscordEmoji.FromName(info.Client, $":{role.Emote}:")))
                        await msg.CreateReactionAsync(DiscordEmoji.FromName(info.Client, $":{role.Emote}:"));
                }
                info.Task = Task.Run(() => UpdateGuild(info));

            }
        }

        private async Task UpdateGuild(GuildInfo info)
        {
            int count = 0;
            string icalData = "";
            var client = new HttpClient();
            while (true)
            {
                if (count == 0)
                {
                    Console.WriteLine("Updating calendar");
                    icalData = await client.GetStringAsync("https://calendar.google.com/calendar/ical/fa0bt70h5crmgq6j9dit61qvks%40group.calendar.google.com/public/basic.ics");
                }
                count++;
                if (count > 10)
                    count = 0;

                Console.WriteLine("Updating discord post");
                var calendar = Ical.Net.Calendar.Load(icalData);
                var lines = info.Template.Replace("\r\n", "\n").Split("\n").ToList();

                for(int i = 0; i < lines.Count; i++)
                {
                    if(lines[i].StartsWith("[") && lines[i].EndsWith("]"))
                    {
                        string valueFilter = lines[i];
                        string valueTemplate = lines[i + 1];
                        lines.RemoveAt(i);
                        lines.RemoveAt(i);

                        var filters = valueFilter.Substring(1, valueFilter.Length - 2).Split("|");
                        IEnumerable<Occurrence> occurrences = null;

                        if(filters[0] == "currentweek")
                            occurrences = calendar.GetOccurrences(StartOfWeek(DateTime.Now, DayOfWeek.Monday).ToUniversalTime(), EndOfWeek(DateTime.Now, DayOfWeek.Monday).ToUniversalTime());
                        else if (filters[0] == "upcomingweek")
                            occurrences = calendar.GetOccurrences(DateTime.Now.ToUniversalTime(), DateTime.Now.AddDays(7).ToUniversalTime());
                        else if (filters[0] == "currentmonth")
                            occurrences = occurrences = calendar.GetOccurrences(DateTime.Now.AddDays(-DateTime.Now.Day).ToUniversalTime(), DateTime.Now.AddDays(-DateTime.Now.Day).AddMonths(1).ToUniversalTime());
                        else
                            Console.WriteLine($"Unknown filter: {filters[0]}");

                        for (int f = 1; f < filters.Length; f++)
                        {
                            if (filters[f] == "withtime")
                                occurrences = occurrences.Where(o => o.Period.StartTime.HasTime);
                            else if (filters[f] == "withouttime")
                                occurrences = occurrences.Where(o => !o.Period.StartTime.HasTime);
                            else if (filters[f] == "withoutlocation")
                                occurrences = occurrences.Where(o => (o.Source as CalendarEvent).Location == "");
                            else if (filters[f].StartsWith("location="))
                            {
                                var loc = filters[f].Substring(9);
                                occurrences = occurrences.Where(o => (o.Source as CalendarEvent).Location.Contains(loc));
                            }
                            else
                                Console.WriteLine($"Unknown filter: {filters[f]}");
                        }


                        int c = 0;
                        foreach(var o in occurrences.OrderBy(o => o.Period.StartTime))
                        {
                            var line = valueTemplate;
                            line = line.Replace("{summary}", (o.Source as CalendarEvent).Summary);


                            while(line.Contains("{"))
                            {
                                int index = line.IndexOf("{");
                                int endIndex = line.IndexOf("}", index);
                                var pre = line.Substring(0, index);
                                var post = line.Substring(endIndex + 1);
                                var tags = line.Substring(index + 1, (endIndex - index) - 1).Split("|");
                                var replacement = "";
                                if (tags[0] == "start")
                                    replacement = FixTime(o.Period.StartTime, tags[1]);
                                else if (tags[0] == "end")
                                    replacement = FixTime(o.Period.EndTime, tags[1]);
                                else if (tags[0] == "active")
                                {
                                    if (o.Period.StartTime.Value < DateTime.Now && o.Period.EndTime.Value > DateTime.Now)
                                        replacement = tags[1];
                                }
                                else if (tags[0] == "eta")
                                {
                                    if(o.Period.StartTime.AsSystemLocal < DateTime.Now)
                                    {
                                        var until = o.Period.StartTime.AsSystemLocal.Subtract(DateTime.Now);
                                        replacement = $"{until.Days} days, {until.Hours} hours, {until.Minutes}minutes";
                                    }
                                    else
                                    {
                                        var until = o.Period.EndTime.AsSystemLocal.Subtract(DateTime.Now);
                                        replacement = $"{until.Days} days, {until.Hours} hours, {until.Minutes}minutes left";
                                    }
                                }
                                else if(tags[0] == "emote")
                                {
                                    var role = info.Roles.Where(r => r.MatchesOccurrence(o)).FirstOrDefault();
                                    if (role == null)
                                        replacement = "calendar";
                                    else
                                        replacement = role.Emote;
                                }
                                else
                                    Console.WriteLine($"Unknown tag: {string.Join(" ", tags)}");


                                line = pre + replacement + post;
                            }



                            lines.Insert(i+c, line);
                            c++;
                        }


                    }
                    if (lines[i] == "{rolelist}")
                    {
                        lines.RemoveAt(i);

                        for (int c = 0; c < info.Roles.Count; c++)
                        {
                            string line = $"Please react with :{info.Roles[c].Emote}: to get notified about {info.Roles[c].DiscordText}";
                            lines.Insert(i + c, line);
                        }

                    }
                }

                var message = await info.Client.Guilds[info.Id].GetChannel(info.ChannelId).GetMessageAsync(info.MessageId);
                var txt = String.Join("\n", lines);
                await message.ModifyAsync(txt);

                
                var upcoming = calendar.GetOccurrences(DateTime.Now.ToUniversalTime(), DateTime.Now.AddDays(1).ToUniversalTime());
                foreach (var r in info.Roles)
                {
                    if (r.MessageId == 0)
                    {
                        foreach (var o in upcoming)
                        {
                            if (r.MatchesOccurrence(o) && o.Period.StartTime.HasTime && o.Period.Contains(new CalDateTime(DateTime.Now)))
                            {
                                r.Occurrence = o;
                                string text = (await info.Client.GetGuildAsync(info.Id)).GetRole(r.Id).Mention + " " + r.Template;
                                text = text.Replace("{name}", (o.Source as CalendarEvent).Summary);
                                var msg = await (await info.Client.GetChannelAsync(info.ChannelId)).SendMessageAsync(text);
                                r.MessageId = msg.Id;
                            }
                        }
                    } 
                    else if(r.MessageId != 0)
                    {
                        if(!r.Occurrence.Period.Contains(new CalDateTime(DateTime.Now)))
                        {
                            await (await (await info.Client.GetChannelAsync(info.ChannelId)).GetMessageAsync(r.MessageId)).DeleteAsync();
                            r.MessageId = 0;
                            r.Occurrence = null;
                        }
                    }
                }





                await Task.Delay(10000);
            }
        }

        public static string FixTime(IDateTime dateTime, string format)
        {
            if (format == "date")
                return CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(dateTime.Month) +
                    " " +
                    ordinal(dateTime.Day);
            else if(format == "datetime")
                return CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(dateTime.Month) +
                    " " +
                    ordinal(dateTime.Day) +
                    ", " +
                    dateTime.Hour.ToString("D2") +
                    ":" +
                    dateTime.Minute.ToString("D2");
            else if(format == "time")
                return
                    dateTime.Hour.ToString("D2") +
                    ":" +
                    dateTime.Minute.ToString("D2");
            else
                return "";
        }



        public static string ordinal(int num)
        {
            var ones = num % 10;
            int tens = (int)Math.Floor((double)(num / 10)) % 10;
            if (tens == 1)
                return num + "th";
            else
            {
                switch (ones)
                {
                    case 1: return num + "st";
                    case 2: return num + "nd";
                    case 3: return num + "rd";
                    default: return num + "th";
                }
            }
        }

        public static DateTime StartOfWeek(DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
        public static DateTime EndOfWeek(DateTime dt, DayOfWeek startOfWeek)
        {
            return StartOfWeek(dt, startOfWeek).AddDays(7);
        }
    }
}
