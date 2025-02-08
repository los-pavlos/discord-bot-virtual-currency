using DSharpPlus;  // Přidej tuto direktivu
using DSharpPlus.Entities;  // Pro přístup k DiscordUser

using Quartz;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BetBuddy
{
    public class LotteryJob : IJob
    {
        private readonly DiscordClient _discord;

        // Konstruktor přijímající DiscordClient
        public LotteryJob(DiscordClient discord)
        {
            _discord = discord;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            Console.WriteLine("Running automatic lottery draw...");

            var db = new Database();
            var entries = await db.GetLotteryEntriesAsync();

            // Check if there are any entries
            if (entries.Count >= 1)
            {
                // Draw the lottery automatically
                Console.WriteLine("Drawing lottery automatically...");
                await DrawLotteryAutomatically(db, entries);
                Console.WriteLine("Automatic lottery draw completed.");
            }
            else
            {
                Console.WriteLine("Not enough participants to draw a lottery.");
            }
        }

        private async Task DrawLotteryAutomatically(Database db, List<(ulong UserId, string Username, long Amount, ulong GuildId, ulong ChannelId)> entries)
        {
            var totalAmount = await db.GetTotalLotteryAmountAsync();

            if (totalAmount == 0)
            {
                Console.WriteLine("No money in the lottery, no winner can be chosen.");
                return;
            }

            // Randomly select a winner
            var random = new Random();
            var randomValue = random.NextDouble() * totalAmount;
            double cumulativeAmount = 0;

            foreach (var entry in entries)
            {
                cumulativeAmount += entry.Amount;

                if (cumulativeAmount >= randomValue)
                {
                    Console.WriteLine($"The winner is {entry.Username} with {entry.Amount} coins!");

                    // Update the winner's balance
                    await db.UpdateBalanceAsync(entry.UserId, entry.Amount + totalAmount);

                    // Retrieve the guild and channel for the winner
                    var guild = await _discord.GetGuildAsync(entry.GuildId); // Get the guild where the lottery took place
                    var channel = guild?.GetChannel(entry.ChannelId); // Get the specific channel

                    if (channel != null)
                    {
                        // Send a message to the channel of the winning server
                        await channel.SendMessageAsync($"🎉 Congratulations, {entry.Username}! You have won the lottery and received {totalAmount} coins!");
                        Console.WriteLine($"Sent message to channel {channel.Name} on guild {guild.Name}");
                    }
                    else
                    {
                        Console.WriteLine("Couldn't find the specified channel.");
                    }

                    // Clear the lottery entries
                    await db.ClearLotteryAsync();
                    return;
                }
            }

            Console.WriteLine("No winner selected (unexpected behavior).");
        }



    }
}
