using DSharpPlus;
using DSharpPlus.Entities;
using Quartz;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading.Tasks;

namespace BetBuddy
{
    public class LotteryJob : IJob
    {
        private readonly DiscordClient _discord;

        public LotteryJob(DiscordClient discord)
        {
            _discord = discord;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            Console.WriteLine("Running automatic lottery draw...");

            var db = new Database();
            var entries = await db.GetLotteryEntriesAsync();

            if (entries.Count >= 1)
            {
                await DrawLotteryAutomatically(db, entries);
            }
            else
            {
                Console.WriteLine("Not enough participants.");
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

            var random = new Random();
            var randomValue = random.NextDouble() * totalAmount;
            long cumulativeAmount = 0;

            

            foreach (var entry in entries)
            {
                cumulativeAmount += entry.Amount;

                if (cumulativeAmount >= randomValue)
                {
                    // aktualni balance vyherce
                    long balance = await db.GetBalanceAsync(entry.UserId);

                    Console.WriteLine($"The winner is {entry.Username} with {entry.Amount} coins!");
                    await db.UpdateBalanceAsync(entry.UserId, balance + totalAmount);

                    var guild = await _discord.GetGuildAsync(entry.GuildId);
                    var channel = guild?.GetChannel(entry.ChannelId);

                    if (guild is not null && channel is not null)
                    {


                        var embed = new DiscordEmbedBuilder
                        {
                            Title = "🎉 Lottery Winner",
                            Color = DiscordColor.Blue
                        }
                        .AddField(":partying_face: :partying_face: :partying_face: :partying_face: :partying_face: ",
                        $"🎉 Congratulations, <@{entry.UserId}>!\nYou have won the lottery and received **{totalAmount:N0}** coins!>\n")
                        .AddField("Your chance to win was", $"{entry.Amount} / {totalAmount} = {Math.Round((double)entry.Amount / totalAmount * 100, 2)}%")
                        .AddField("Total participants", entries.Count.ToString());





                        //  send embed to the channel

                        await channel.SendMessageAsync(embed: embed);

                        Console.WriteLine($"Sent message to channel {channel.Name} on guild {guild.Name}");
                    }
                    else
                    {
                        Console.WriteLine("Couldn't find the specified channel.");
                    }

                    await db.ClearLotteryAsync();
                    return;
                }
            }

            Console.WriteLine("No winner selected (unexpected behavior).");
        }
    }
}
