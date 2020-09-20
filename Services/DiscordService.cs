using DiscordEventBot.Models.db;
using DSharpPlus;
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

    class GuildInfo
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; internal set; }
        public Task Task { get; internal set; }
        public string CalendarUrl { get; internal set; }
        public BaseDiscordClient Client { get; internal set; }
        public string Template { get; internal set; }
    }

    class DiscordInfo
    {
        public int DiscordBotId { get; set; }
        public Dictionary<ulong, GuildInfo> Guilds { get; set; } = new Dictionary<ulong, GuildInfo>();
    }

    public class DiscordService : BackgroundService
    {
        private ILogger<DiscordService> logger;
        private HttpClient client;
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

        private Task ReactionCleared(MessageReactionsClearEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task ReactionRemoved(MessageReactionRemoveEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task ReactionAdded(MessageReactionAddEventArgs e)
        {
            return Task.CompletedTask;
        }

        private async Task GuildAvailable(GuildCreateEventArgs e)
        {
            using (var context = new EventContext())
            {
                var botId = clients.FirstOrDefault(x => x.Value == e.Client).Key;
                var bot = context.DiscordBots.Include(b => b.GuildConfigs).FirstOrDefault(b => b.Id == botId);
                
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
                    Client = e.Client,
                    Id = e.Guild.Id,
                    Name = e.Guild.Name,
                    ChannelId = e.Guild.Channels.FirstOrDefault(c => c.Name == config.EventChannelName.ToLower()).Id,
                    CalendarUrl = config.CalendarUrl,
                    Template = config.Template
                };
                discordInfo[e.Client].Guilds[e.Guild.Id] = info;

                var channel = e.Guild.GetChannel(info.ChannelId);
                var messages = await channel.GetMessagesAsync();
                for(int i = 0; i < messages.Count; i++)
                {
                    if (messages[i].Author.IsCurrent)
                        info.MessageId = messages[i].Id;
                }
                if (info.MessageId == 0)
                    info.MessageId = (await channel.SendMessageAsync("Bot is starting...")).Id;
                else
                    await (await channel.GetMessageAsync(info.MessageId)).ModifyAsync("Bot is starting...");


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
                            occurrences = calendar.GetOccurrences(StartOfWeek(DateTime.Now, DayOfWeek.Monday), EndOfWeek(DateTime.Now, DayOfWeek.Monday));
                        else if (filters[0] == "upcomingweek")
                            occurrences = calendar.GetOccurrences(DateTime.Now, DateTime.Now.AddDays(7));
                        else if (filters[0] == "currentmonth")
                            occurrences = occurrences = calendar.GetOccurrences(DateTime.Now.AddDays(-DateTime.Now.Day), DateTime.Now.AddDays(-DateTime.Now.Day).AddMonths(1));
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
                                    var until = o.Period.StartTime.AsSystemLocal.Subtract(DateTime.Now);
                                    replacement = $"{until.Days} days, {until.Hours} hours, {until.Minutes}minutes";
                                }
                                else
                                    Console.WriteLine($"Unknown tag: {tags}");


                                line = pre + replacement + post;
                            }



                            lines.Insert(i+c, line);
                            c++;
                        }


                    }
                }




                var message = await info.Client.Guilds[info.Id].GetChannel(info.ChannelId).GetMessageAsync(info.MessageId);
                var txt = String.Join("\n", lines);
                Console.WriteLine(txt);
                await message.ModifyAsync(txt);
                await Task.Delay(60000);
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
