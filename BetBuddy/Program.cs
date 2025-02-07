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


/*
 * Discord bot that allows users to play games and earn virtual currency.
 * Users can check their balance, play coin flip, enter the lottery, claim daily rewards, work, and play rock-paper-scissors.
 * The bot uses SQLite to store user data.
 * 
 * Commands:
 * - bb money: Check your balance
 * - bb addmoney <player> <amount>: Add money to a player's balance
 * - bb cf <bet>: Play coin flip
 * - bb lottery [amount]: Enter the lottery or view current participants
 * - bb drawlottery: Draw the lottery winner
 * - bb daily: Claim your daily reward
 * - bb work: Work to earn coins
 * - bb rps <choice> <bet>: Play rock-paper-scissors
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
 * 3. Run the program
 */




namespace ForexCastBot
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: false));

            string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("❌ ERROR: Discord token not found! check .env file.");
                return;
            }

            Console.WriteLine("✅ Token sucesfully loaded: " + token.Substring(0, 5) + "*****");

            var discord = new DiscordClient(new DiscordConfiguration()
            {
                Token = token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.All
            });

            var commands = discord.UseCommandsNext(new CommandsNextConfiguration()
            {
                StringPrefixes = new[] { "bb " },
                IgnoreExtraArguments = true,
                EnableDefaultHelp = false
            });

            commands.RegisterCommands<BotCommands>();


            await discord.ConnectAsync();
            await Task.Delay(5000); // Wait for the bot to connect

            var activities = new List<DiscordActivity>
            {
                new DiscordActivity("lottery status", ActivityType.Watching),
                new DiscordActivity("Server status", ActivityType.Playing),
                //new DiscordActivity("your losses 💸", ActivityType.Watching),
                //new DiscordActivity("your debt 📻", ActivityType.ListeningTo),
                
                //new DiscordActivity("the odds 🤫", ActivityType.Playing),
                //new DiscordActivity("bad bets 📉", ActivityType.Watching),
                

            };

            int index = 0;

            while (true) // infinite loop for changing activities
            {
                var activity = activities[index];
                await discord.UpdateStatusAsync(activity, UserStatus.Online);

                /* Lottery and serverCounts status activity update */
                Database db = new Database();
                var entries = await db.GetLotteryEntriesAsync();
                var totalAmount = await db.GetTotalLotteryAmountAsync();
                var entriesCount = entries.Count;
                if (entriesCount == 0)
                {
                    activities[0] = new DiscordActivity($"there is not lottery in progress", ActivityType.Watching);
                }
                else
                {
                    activities[0] = new DiscordActivity($"the lottery 🎰 ({entries.Count}/5) - {totalAmount} coins", ActivityType.Watching);
                }

                int serverCount = discord.Guilds.Count;
                
                activities[1] = new DiscordActivity($"on {serverCount} servers! 🌍", ActivityType.Playing);


                index = (index + 1) % activities.Count; // Move to the next activity
                await Task.Delay(90000); //  wait
            }
        }
    }
        public class BotCommands : BaseCommandModule
    {
        private static readonly HttpClient client = new HttpClient();

        ulong adminId = 409818422344417293;  // Admin ID


        [Command("help")]
        public async Task Help(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder
            {
                Title = "📜 Available Commands",
                Color = DiscordColor.Azure
            }
            .AddField("💰 Economy", "`bb money` - Check your balance\n`bb daily` - Claim your daily reward\n`bb work` - Claim your work reward\n`bb leaderboard` - Check top 10 richest players")
            .AddField("🎮 Games", "`bb rps <choice> <bet>` - Rock Paper Scissors\n`bb cf <bet>` - Coin Flip\n`bb roulette <choice> <bet>` - Roulette")
            .AddField("🎟 Lottery", "`bb lottery <amount>` - Join the lottery");
            

            await ctx.RespondAsync(embed);
        }

        // Command to view the player's balance
        [Command("money")]
        public async Task Money(CommandContext ctx)
        {
            string username = ctx.User.Username;
            Database db = new Database();
            long balance = await db.GetBalanceAsync(username);
            await ctx.RespondAsync($"💰 {username}, your current balance is: **{balance}** coins.");
        }

        // Command to add money to a player's balance (admin only)
        [Command("addmoney")]
        public async Task AddMoney(CommandContext ctx, string playerUsername, long amount)
        {
            if (amount <= 0)
            {
                await ctx.RespondAsync("⚠️ Please enter a valid amount greater than zero.");
                return;
            }

            Database db = new Database();
            bool playerExists = await db.PlayerExistsAsync(playerUsername);
            if (!playerExists)
            {
                await db.AddPlayerAsync(playerUsername);
            }

            long currentBalance = await db.GetBalanceAsync(playerUsername);
            long newBalance = currentBalance + amount;
            await db.UpdateBalanceAsync(playerUsername, newBalance);

            await ctx.RespondAsync($"✅ **{amount}** coins have been added to {playerUsername}'s balance.\nNew balance: **{newBalance}** coins.");
        }

        // Command for the coin flip game
        [Command("cf")]
        public async Task CoinFlip(CommandContext ctx, string betString)
        {
            string username = ctx.User.Username;
            Database db = new Database();
            long playerBalance = await db.GetBalanceAsync(username);
            long bet;

            bool playerExists = await db.PlayerExistsAsync(username);
            if (!playerExists)
            {
                await db.AddPlayerAsync(username);
            }

            if (betString == "all")
            {
                bet = playerBalance;
            }
            else if (!long.TryParse(betString, out bet) || bet <= 0)
            {
                await ctx.RespondAsync("⚠️ Please enter a valid bet amount greater than zero.");
                return;
            }

            if (playerBalance < bet|| playerBalance == 0)
            {
                await ctx.RespondAsync($"⚠️ {username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance}** coins.");
                return;
            }

            Random random = new Random();
            bool isWin = random.Next(0, 2) == 0;

            if (isWin)
            {
                playerBalance += bet;
                await ctx.RespondAsync($"🎉 HEADS! You win **{bet}** coins! New balance: **{playerBalance}** coins.");
            }
            else
            {
                playerBalance -= bet;
                await ctx.RespondAsync($"😢 TAILS! You lose **{bet}** coins. New balance: **{playerBalance}** coins.");
            }

            await db.UpdateBalanceAsync(username, playerBalance);
        }


        // Command to show current lottery participants and total amount
        // Command to enter the lottery and show the current participants
        [Command("lottery")]
        public async Task Lottery(CommandContext ctx, string? amount = null)
        {
            string username = ctx.User.Username;
            Database db = new Database();

            string participantsMessage = "";
            long playerBalance = await db.GetBalanceAsync(username);
            long bet=0;

            bool playerExists = await db.PlayerExistsAsync(username);
            if (!playerExists)
            {
                await db.AddPlayerAsync(username);
            }

            // if there is a bet amount, add the user to the lottery
            if (amount != null)
            {
                if (amount == "all")
                {
                    bet = playerBalance;
                }
                else if (!long.TryParse(amount, out long betAmount) || betAmount <= 0)
                {
                    await ctx.RespondAsync("⚠️ Please enter a valid amount greater than zero.");
                    return;
                }else
                {
                    bet = betAmount;
                }


                if (playerBalance < bet)
                {
                    await ctx.RespondAsync($"⚠️ {username}, you don't have enough coins to bet that amount. Your current balance is **{playerBalance}** coins.");
                    return;
                }

                // Add the user to the lottery
                await db.AddLotteryEntryAsync(username, bet);

                // Update the user's balance
                await db.UpdateBalanceAsync(username, playerBalance - bet);

                participantsMessage += ($"✅ {username}, you have successfully entered the lottery with **{bet}** coins.\n");
            }

            // Get all lottery entries
            var entries = await db.GetLotteryEntriesAsync();
            var totalAmount = await db.GetTotalLotteryAmountAsync();

            if (entries.Count == 0)
            {
                await ctx.RespondAsync("⚠️ There are no participants in the lottery yet.");
                return;
            }

            // Create a message with all participants and their chances
            participantsMessage += $"🎉 Current Lottery Participants **({entries.Count}/5)**:\n";
            foreach (var entry in entries)
            {
                double chance = (double)entry.Amount / totalAmount * 100;
                participantsMessage += $"- **{entry.Username}**: {entry.Amount} coins | **{chance:F2}%** chance\n";
            }

            participantsMessage += $"Total Lottery Pool: **{totalAmount}** coins.";
            await ctx.RespondAsync(participantsMessage);

            // Check if there are 5 participants, if so, automatically trigger the draw
            if (entries.Count >= 5)
            {
                await DrawLotteryAutomatically(ctx);
            }
        }

        // Command to manually draw the lottery (admin only)
        [Command("drawlottery")]
        public async Task DrawLottery(CommandContext ctx)
        {
            // Zkontroluj, zda má uživatel správné ID (admin ID)
            if (ctx.User.Id != adminId)
            {
                await ctx.RespondAsync("⚠️ You do not have permission to perform this action.");
                return;
            }

            // Zavolání metody pro losování, pokud je admin
            await DrawLotteryAutomatically(ctx);
        }

        // Automatically draw the lottery when there are enough participants or triggered manually
        public async Task DrawLotteryAutomatically(CommandContext ctx)
        {
            string username = ctx.User.Username;
            Database db = new Database();

            // Get all lottery entries
            var entries = await db.GetLotteryEntriesAsync();
            if (entries.Count == 0)
            {
                await ctx.RespondAsync("⚠️ No one has entered the lottery.");
                return;
            }

            // Calculate the total amount of coins in the lottery
            long totalAmount = 0;
            foreach (var entry in entries)
            {
                totalAmount += entry.Amount;
            }

            // Choose a random winner based on a random number
            Random random = new Random();
            long randomNumber = random.Next(0, (int)totalAmount);

            // Find the winner based on the random number
            long accumulatedAmount = 0;
            string winner = "";
            long winnerAmount = 0; // Store the winner's amount here for later calculation
            foreach (var entry in entries)
            {
                accumulatedAmount += entry.Amount;
                if (accumulatedAmount >= randomNumber)
                {
                    winner = entry.Username;
                    winnerAmount = entry.Amount; // Store the winner's amount
                    break;
                }
            }

            // Calculate the winner's percentage chance
            double winnerChance = (double)winnerAmount / totalAmount * 100;

            // Announce the winner
            await ctx.RespondAsync($"🎉 The winner of the **{totalAmount}** coin lottery is **{winner}**! Congratulations!\n📊 Winner had **{winnerChance:F2}%** chance of winning");

            // Add the total amount to the winner's balance
            long currentBalance = await db.GetBalanceAsync(winner);
            await db.UpdateBalanceAsync(winner, currentBalance + totalAmount);

            // Clear the lottery entries after the draw
            await db.DeleteOldLotteryEntriesAsync(DateTime.UtcNow);
        }

        [Command("daily")]
        public async Task Daily(CommandContext ctx)
        {
            string username = ctx.User.Username;
            Database db = new Database();
            
            if(!await db.PlayerExistsAsync(username))
            {
                await db.AddPlayerAsync(username);
            }


            DateTime? lastClaimed = await db.GetLastClaimedAsync(username);
            DateTime today = DateTime.UtcNow.Date; // Today's date

            // If the user hasn't claimed their daily reward yet today
            if (!lastClaimed.HasValue || lastClaimed.Value.Date < today)
            {
                Random random = new Random();
                long reward = random.Next(500, 1501); // Reward between 500 and 1500
                long currentBalance = await db.GetBalanceAsync(username);
                long newBalance = currentBalance + reward;

                await db.UpdateBalanceAsync(username, newBalance);
                await db.UpdateLastClaimedAsync(username); // Update the last claimed time

                await ctx.RespondAsync($"🎉 {username}, you claimed your daily reward of **{reward}** coins! New balance: **{newBalance}** coins.");
            }
            else
            {
                // Calculate the remaining time until midnight
                var remainingTime = today.AddDays(1) - DateTime.UtcNow; // Time until midnight
                await ctx.RespondAsync($"⚠️ {username}, you can claim your daily reward again tomorrow at midnight! Remaining time: **{remainingTime:hh\\:mm\\:ss}**.");
            }
        }

        [Command("work")]
        public async Task Work(CommandContext ctx)
        {
            string username = ctx.User.Username;
            Database db = new Database();
            long currentBalance = await db.GetBalanceAsync(username);

            if (!await db.PlayerExistsAsync(username))
            {
                await db.AddPlayerAsync(username);
            }

            if (currentBalance == 0)
            {
                Random random = new Random();
                long reward = random.Next(20, 101); // Reward between 20 and 100

                long newBalance = currentBalance + reward;

                await db.UpdateBalanceAsync(username, newBalance);

                await ctx.RespondAsync($"💼 **{username}**, you worked hard and earned **{reward}** coins! ✨ Your new balance is: **{newBalance}** coins. 🏅");
                return;
            }
            else
            {
                await ctx.RespondAsync($"⚠️ **{username}**, you can't work if you already have money. 💰 But don't worry, you can still try your luck in the casino! 🎰");
                return;
            }
        }


        [Command("rps")]
        public async Task RPS(CommandContext ctx, string playerChoice, string betString)
        {
            // get username
            string username = ctx.User.Username;
            Database db = new Database();

            long bet;  // Change bet to long

            // get player balance
            long playerBalance = await db.GetBalanceAsync(username);

            if (!await db.PlayerExistsAsync(username))
            {
                await db.AddPlayerAsync(username);
            }

            if (betString == "all")
            {
                bet = playerBalance;
            }
            else if (!long.TryParse(betString, out long bett) || bett <= 0)  // Check if bet is a valid long
            {
                await ctx.RespondAsync("⚠️ Please enter a valid bet amount greater than zero.");
                return;
            }
            else
            {

                bet = bett;
            }

            Console.WriteLine(ctx.User.Username + " RPS");

            // check if player has enough balance
            if (playerBalance < bet)
            {
                await ctx.RespondAsync($"⚠️ {username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance}** coins.");
                return;
            }

            // valid choices
            string[] validChoices = { "rock", "paper", "scissors" };

            // check if player choice is valid
            if (!Array.Exists(validChoices, choice => choice == playerChoice.ToLower()))
            {
                await ctx.RespondAsync("⚠️ Please enter a valid choice: rock, paper, or scissors.");
                return;
            }

            // computer choice
            Random random = new Random();
            int computerChoiceIndex = random.Next(0, 3);
            string computerChoice = computerChoiceIndex switch
            {
                0 => "rock",
                1 => "paper",
                2 => "scissors",
                _ => throw new InvalidOperationException()
            };

            // game logic
            string result = "";
            if (playerChoice.ToLower() == computerChoice)
            {
                result = "**It's a tie!**";
            }
            else if ((playerChoice.ToLower() == "rock" && computerChoice == "scissors") ||
                     (playerChoice.ToLower() == "scissors" && computerChoice == "paper") ||
                     (playerChoice.ToLower() == "paper" && computerChoice == "rock"))
            {
                result = "**You win!**";
                playerBalance += bet;  // Player wins, add bet to balance
            }
            else
            {
                result = "**Computer wins!**";
                playerBalance -= bet;  // Computer wins, subtract bet from balance
            }

            // update player balance
            await db.UpdateBalanceAsync(username, playerBalance);

            // send result message
            string message = $"You chose: **{playerChoice.ToLower()}** with **{bet}** coins bet\n" +
                             $"Computer chose: **{computerChoice}**\n" +
                             $"{result}\n" +
                             $"Your new balance is: **{playerBalance}** coins.";
            await ctx.RespondAsync(message);
        }

        [Command("leaderboard")]
        public async Task Leaderboard(CommandContext ctx)
        {
            Database db = new Database();
            string username = ctx.User.Username;
            // Získání všech top hráčů
            var topPlayers = await db.GetTopPlayersAsync();

            if (!await db.PlayerExistsAsync(username))
            {
                await db.AddPlayerAsync(username);
            }

            if (topPlayers.Count == 0)
            {
                await ctx.RespondAsync("⚠️ There are no players with a balance yet.");
                return;
            }

            // Vytvoření zprávy pro leaderboard
            string leaderboardMessage = "🏆 **Leaderboard - Top 10 Richest Players** 🏆\n";
            int rank = 1;
            foreach (var player in topPlayers)
            {
                leaderboardMessage += $"{rank}. **{player.Username}**: {player.Balance} coins\n";
                rank++;
                if (rank > 10) break; // Zobrazíme pouze top 10 hráčů
            }

            await ctx.RespondAsync(leaderboardMessage);
        }

        [Command("removeplayer")]
        public async Task RemovePlayer(CommandContext ctx, string playerUsername)
        {



            // check if user has admin permissions
            if (ctx.User.Id != adminId)
            {
                await ctx.RespondAsync("⚠️ You do not have permission to perform this action.");
                return;
            }

            Database db = new Database();

            // check if player exists
            bool playerExists = await db.PlayerExistsAsync(playerUsername);
            if (!playerExists)
            {
                await ctx.RespondAsync($"⚠️ Player **{playerUsername}** does not exist in the database.");
                return;
            }

            // remove player
            await db.RemovePlayerAsync(playerUsername);

            await ctx.RespondAsync($"✅ Player **{playerUsername}** has been successfully removed from the database.");
        }


        [Command("printplayers")]
        public async Task printPlayers(CommandContext ctx)
        {
            // check if user has admin permissions
            if (ctx.User.Id != adminId)
            {
                await ctx.RespondAsync("⚠️ You do not have permission to perform this action.");
                return;
            }

            Database db = new Database();
            await db.PrintPlayersAsync();
        }

        [Command("roulette")]
        public async Task Roulette(CommandContext ctx, string betType, string betAmountString)
        {
            string username = ctx.User.Username;
            Database db = new Database();
            long playerBalance = await db.GetBalanceAsync(username);
            long betAmount;

            // Pokud hráč neexistuje v databázi, přidáme ho
            bool playerExists = await db.PlayerExistsAsync(username);
            if (!playerExists)
            {
                await db.AddPlayerAsync(username);
            }

            // Pokud hráč zadá "all", vsadí celý svůj balance
            if (betAmountString == "all")
            {
                betAmount = playerBalance;
            }
            else if (!long.TryParse(betAmountString, out betAmount) || betAmount <= 0)
            {
                await ctx.RespondAsync("⚠️ Please enter a valid bet amount greater than zero.");
                return;
            }

            // Kontrola, jestli hráč má dost peněz na sázku
            if (playerBalance < betAmount || playerBalance == 0)
            {
                await ctx.RespondAsync($"⚠️ {username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance}** coins.");
                return;
            }

            // Vytvoření číselníku pro barvy rulety
            var colors = new Dictionary<int, string>()
    {
        {0, "green"}, {1, "red"}, {2, "black"}, {3, "red"}, {4, "black"}, {5, "red"}, {6, "black"},
        {7, "red"}, {8, "black"}, {9, "red"}, {10, "black"}, {11, "black"}, {12, "red"}, {13, "black"},
        {14, "red"}, {15, "black"}, {16, "red"}, {17, "black"}, {18, "red"}, {19, "red"}, {20, "black"},
        {21, "red"}, {22, "black"}, {23, "red"}, {24, "black"}, {25, "red"}, {26, "black"}, {27, "red"},
        {28, "black"}, {29, "black"}, {30, "red"}, {31, "black"}, {32, "red"}, {33, "black"}, {34, "red"},
        {35, "black"}, {36, "red"}
    };

            betType = betType.ToLower();
            bool isNumberBet = int.TryParse(betType, out int betNumber);

            if (!isNumberBet && !new[] { "red", "black", "green" }.Contains(betType))
            {
                await ctx.RespondAsync("❌ Invalid bet! You can bet on a **color (red/black/green)** or a **number (0-36)**.");
                return;
            }

            // Ruleta - generování náhodného čísla
            Random rand = new Random();
            int rolledNumber = rand.Next(37);
            string rolledColor = colors[rolledNumber];

            string resultMessage = $"🎡 The roulette wheel spins... **{rolledNumber}** ({rolledColor})! ";

            if (isNumberBet && betNumber == rolledNumber)
            {
                long winnings = betAmount * 35;
                playerBalance += winnings;
                resultMessage += $"🎉 **Jackpot!** You win **{winnings}💰**!";
            }
            else if (!isNumberBet && betType == rolledColor)
            {
                long winnings = (betType == "green") ? betAmount * 35 : betAmount * 2;
                playerBalance += winnings;
                resultMessage += $"✅ You win **{winnings}💰**!";
            }
            else
            {
                playerBalance -= betAmount;
                resultMessage += "❌ You lose!";
            }

            await db.UpdateBalanceAsync(username, playerBalance);
            resultMessage += $" Your new balance: **{playerBalance}💰**.";

            await ctx.RespondAsync(resultMessage);
        }

    }
}