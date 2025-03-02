using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Newtonsoft.Json.Linq;
using System.Data.SQLite;
using System.IO;
using dotenv.net;
using System.Numerics;
using DSharpPlus.Entities;
using Quartz.Impl;
using Quartz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/*
 * Discord bot that allows users to play games and earn virtual currency.
 * Users can check their balance, play coin flip, enter the lottery, claim daily rewards, work, and play rock-paper-scissors.
 * The bot uses SQLite to store user data.
 * 
 * Commands:
 * - bb money: Check your balance
 * - bb addmoney <player> <amount>: Add money to a player's balance
 * - bb bb give <player> <amount>: Give money to a player
 * - bb cf <bet>: Play coin flip
 * - bb lottery [amount]: Enter the lottery or view current participants
 * - bb drawlottery: Draw the lottery winner
 * - bb daily: Claim your daily reward
 * - bb work: Work to earn coins
 * - bb rps <choice> <bet>: Play rock-paper-scissors
 * - bb rulette <choice> <bet>: Play roulette
 * 
 *  add package DSharpPlus
 *  add package DSharpPlus.CommandsNext
 *  add package dotenv.net
 *  add package Microsoft.Data.Sqlite
 *  add package Newtonsoft.Json
 * 
 * To run the bot:
 * 1. Create a .env file
 * 2. Add DISCORD_TOKEN=token to the .env file
 * 4. Enter your discord id to the AdminId variable in BotCommands.cs
 * 3. Run the program
 */


namespace BetBuddy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<BotService>(); // registration of the BotService
                })
                .Build();

            var bot = host.Services.GetRequiredService<BotService>();
            await bot.StartAsync(); // Start the bot
            await Task.Delay(-1); // Prevent the application from closing
        }
    }
}
