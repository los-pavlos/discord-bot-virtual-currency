using dotenv.net;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Net;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BetBuddy
{
    public class BotService
    {
        // = null! because we know it will be initialized in the constructor and it will avoid null reference exceptions warnings
        private readonly DiscordClient _discord = null!;
        private readonly CommandsNextExtension _commands = null!;
        private readonly IScheduler _scheduler = null!;


        public BotService()
        {
            // token loading
            DotEnv.Load();
            string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("❌ ERROR: Discord token not found!");
                return;
            }

            // setup discord client
            _discord = new DiscordClient(new DiscordConfiguration()
            {
                Token = token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.All
            });

            // setup commands
            _commands = _discord.UseCommandsNext(new CommandsNextConfiguration()
            {
                StringPrefixes = new[] { "bb " },
                IgnoreExtraArguments = true,
                EnableDefaultHelp = false
            });

            _commands.RegisterCommands<BotCommands>();

            // initialize scheduler
            _scheduler = new StdSchedulerFactory().GetScheduler().Result;
            _scheduler.JobFactory = new LotteryJobFactory(_discord);
        }

        public async Task StartAsync()
        {
            await _discord.ConnectAsync();
            Console.WriteLine("✅ Bot connected!");


            await ScheduleLotteryJob();
            await UpdateActivityLoop();
        }

        private async Task ScheduleLotteryJob()
        {
            IJobDetail job = JobBuilder.Create<LotteryJob>()
                .WithIdentity("LotteryJob", "group1")
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity("LotteryTrigger", "Group1")
                //.WithCronSchedule("0/10 * * * * ?")  // every 10 seconds (for testing)
                .WithCronSchedule("0 0 20 * * ?")  // every day at 20:00
                .Build();

            await _scheduler.ScheduleJob(job, trigger);
            await _scheduler.Start();
            Console.WriteLine("✅ LotteryJob planned!");
        }

        //  Check the next lottery close time
        private async Task<DateTime> GetNextLotteryCloseTimeAsync()
        {
            // Create a trigger key
            var triggerKey = new Quartz.TriggerKey("LotteryTrigger", "Group1");

            // Get the trigger by its key
            var trigger = await _scheduler.GetTrigger(triggerKey, default);

            // If the trigger is null, it means the lottery job is not scheduled correctly
            if (trigger == null)
            {
                throw new Exception("Lottery job is not scheduled correctly.");
            }

            // Get the next fire time of the trigger
            var nextFireTime = trigger.GetNextFireTimeUtc();

            // If the next fire time is null, it means the lottery job is not scheduled correctly
            if (nextFireTime == null)
            {
                throw new Exception("No next fire time found for the lottery job.");
            }

            // Return the next fire time in the local time zone
            return nextFireTime.Value.LocalDateTime;
        }

        // Loop to update the bot's activity
        private async Task UpdateActivityLoop()
        {
            Database db = new Database();

            while (true)
            {
                // Activity data
                long lotteryPool = await db.GetTotalLotteryAmountAsync();
                DateTime lotteryCloseTime = await GetNextLotteryCloseTimeAsync();
                TimeSpan timeRemaining = lotteryCloseTime - DateTime.Now;
                string timeRemainingFormatted = $"{timeRemaining.Hours}h {timeRemaining.Minutes}m";

                // Count servers
                int serversCount = _discord.Guilds.Count;

                // Create a list of activities
                var activities = new List<DiscordActivity>
                {
                    new DiscordActivity($"lottery in {timeRemainingFormatted} with {lotteryPool:N0} coins", ActivityType.Playing),
                    new DiscordActivity($"on {serversCount} servers", ActivityType.Playing),
                    new DiscordActivity($"bb help", ActivityType.ListeningTo)
                };

                // Switch activities
                for (int i = 0; i < activities.Count; i++)
                {
                    await _discord.UpdateStatusAsync(activities[i], UserStatus.Online);
                    await Task.Delay(60000); // Wait 1 minute
                }
            }
        }

    }
}
