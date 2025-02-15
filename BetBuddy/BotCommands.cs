using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Lavalink;
using DSharpPlus.VoiceNext;
using DSharpPlus.Entities;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Collections.Concurrent;

namespace BetBuddy
{
    public class BotCommands : BaseCommandModule
    {
        private static readonly HttpClient client = new HttpClient();

        ulong adminId = 409818422344417293;  // Admin ID

        //  dictionary to store the last time a command was used by a user
        private static Dictionary<(ulong, string), DateTime> LastCommandUsage = new();
        private static Dictionary<string, TimeSpan> CommandCooldowns = new()
        {
            { "work", TimeSpan.FromSeconds(60) } // work command can be used once per minute
        };
        private static TimeSpan DefaultCooldown = TimeSpan.FromSeconds(5);

        private async Task<bool> CheckCooldown(CommandContext ctx, string commandName)
        {
            var key = (ctx.User.Id, commandName);

            // use specified cooldown if it exists, otherwise use the default cooldown
            TimeSpan cooldownDuration = CommandCooldowns.ContainsKey(commandName)
                ? CommandCooldowns[commandName]
                : DefaultCooldown;

            if (LastCommandUsage.TryGetValue(key, out DateTime lastUsed))
            {
                TimeSpan elapsed = DateTime.UtcNow - lastUsed;
                if (elapsed < cooldownDuration)
                {
                    double remaining = (cooldownDuration - elapsed).TotalSeconds;
                    var cooldownMsg = await ctx.RespondAsync($"⏳ Please wait **{remaining:N0}** seconds before using `{commandName}` again...");

                    while (remaining > 0)
                    {
                        await Task.Delay(1000);
                        remaining--;
                        await cooldownMsg.ModifyAsync($"⏳ Please wait **{remaining:F1}** seconds before using `{commandName}` again...");
                    }

                    await cooldownMsg.DeleteAsync();
                    return false; // cooldown hasn't expired yet
                }
            }

            // cooldown has expired, update the last command usage
            LastCommandUsage[key] = DateTime.UtcNow;
            return true;
        }


        [Command("help")]
        public async Task Help(CommandContext ctx)
        {
            if (!await CheckCooldown(ctx, "help")) return; // command cooldown

            var embed = new DiscordEmbedBuilder
            {
                Title = "📜 Available Commands",
                Color = DiscordColor.Blue
            }
            .AddField("💰 Economy", "`bb money` - Check your balance\n`bb daily` - Claim your daily reward\n`bb work` - Claim your work reward\n`bb leaderboard` - Check top 10 richest players")
            .AddField("🎮 Games", "`bb rps <choice> <bet>` - Rock Paper Scissors\n`bb cf <bet>` - Coin Flip\n`bb roulette <choice> <bet>` - Roulette")
            .AddField("🎟 Lottery", "`bb lottery <amount>` - Join the lottery (drawn daily)")
            .AddField("🔗 Invite", "[Click here to invite the bot](https://discord.com/oauth2/authorize?client_id=1336641695575572490&permissions=277025459200&integration_type=0&scope=bot)");

        
            await ctx.RespondAsync(embed);
        }

        [Command("money")]
        public async Task Money(CommandContext ctx)
        {
            if (!await CheckCooldown(ctx, "money")) return; // command cooldown

            ulong userId = ctx.User.Id;
            Database db = new Database();
            long balance = await db.GetBalanceAsync(userId);
            await ctx.RespondAsync($"💰 <@{userId}>, your current balance is: **{balance:N0}** coins.");
            Console.WriteLine($"User {userId} checked their balance.");
        }


        [Command("give")]
        public async Task Give(CommandContext ctx, string playerMention, long amount)
        {
            if (!await CheckCooldown(ctx, "give")) return; // command cooldown

            if (amount <= 0)
            {
                await ctx.RespondAsync("⚠️ Please enter a valid amount greater than zero.");
                return;
            }

            // extract player ID from mention
            ulong playerId;
            if (!ulong.TryParse(playerMention.Trim('<', '@', '>'), out playerId))
            {
                await ctx.RespondAsync("⚠️ Invalid mention format.");
                return;
            }

            Database db = new Database();
            // check if player exists
            bool playerExists = await db.PlayerExistsAsync(playerId);
            if (!playerExists)
            {
                await ctx.RespondAsync("⚠️ This player doesn't exist in our database.");
                return;
            }
            ulong userId = ctx.User.Id;
            long balance = await db.GetBalanceAsync(userId);
            if (balance < amount)
            {
                await ctx.RespondAsync($"⚠️ You don't have enough money. Your curent balance is {balance:NO}");
                return;
            }
            balance -= amount;
            await db.UpdateBalanceAsync(userId, balance);


            // get current balance
            long currentBalance = await db.GetBalanceAsync(playerId);
            long newBalance = currentBalance + amount;
            // update balance
            await db.UpdateBalanceAsync(playerId, newBalance);
            // mention the player
            var mention = $"<@{playerId}>";
            // send response
            await ctx.RespondAsync($"✅ **{amount:N0}** coins have been sent to {mention}.\nNew balance: **{newBalance:N0}** coins.");
        }


        [Command("addmoney")]
        public async Task AddMoney(CommandContext ctx, string playerMention, long amount)
        {
            if (!await CheckCooldown(ctx, "addmoney")) return; // command cooldown

            if (ctx.User.Id != adminId)
            {
                await ctx.RespondAsync("⚠️ You do not have permission to perform this action.");
                return;
            }

            
            if (amount <= 0)
            {
                await ctx.RespondAsync("⚠️ Please enter a valid amount greater than zero.");
                return;
            }

            // extract player ID from mention
            ulong playerId;
            if (!ulong.TryParse(playerMention.Trim('<', '@', '>'), out playerId))
            {
                await ctx.RespondAsync("⚠️ Invalid mention format.");
                return;
            }

            Database db = new Database();

            // check if player exists
            bool playerExists = await db.PlayerExistsAsync(playerId);
            if (!playerExists)
            {
                await ctx.RespondAsync("⚠️ This player doesn't exist in our database.");
                return;
            }

            // get current balance
            long currentBalance = await db.GetBalanceAsync(playerId);
            long newBalance = currentBalance + amount;

            // update balance
            await db.UpdateBalanceAsync(playerId, newBalance);

            // mention the player
            var mention = $"<@{playerId}>";

            // send response
            await ctx.RespondAsync($"✅ **{amount:N0}** coins have been added to {mention}'s balance.\nNew balance: **{newBalance:N0}** coins.");
        }

        private static string GetTimeRemainingUntilLottery()
        {
            var now = DateTime.Now;
            var nextLotteryTime = DateTime.Today.AddHours(20); // 20:00 today
            if (now > nextLotteryTime)
            {
                nextLotteryTime = nextLotteryTime.AddDays(1); // if it's already past 20:00, set the time to tomorrow's 20:00
            }

            var remainingTime = nextLotteryTime - now;
            return remainingTime.ToString(@"h\h\ m\m");
        }

        [Command("lottery")]
        public async Task Lottery(CommandContext ctx, string? amount = null)
        {
            if (!await CheckCooldown(ctx, "lottery")) return; // command cooldown

            ulong userId = ctx.User.Id;
            string username = ctx.User.Username;
            Database db = new Database();

            long playerBalance = await db.GetBalanceAsync(userId);
            long bet = 0;

            bool playerExists = await db.PlayerExistsAsync(userId);
            if (!playerExists)
            {
                await db.AddPlayerAsync(userId, username);
            }

            if (amount != null)
            {
                if (amount == "all")
                {
                    bet = playerBalance;
                    if(bet == 0)
                    {
                        await ctx.RespondAsync("⚠️ You don't have any coins to enter the lottery.");
                        return;
                    }
                }
                else if (!long.TryParse(amount, out long betAmount) || betAmount <= 0)
                {
                    await ctx.RespondAsync("⚠️ Please enter a valid amount greater than zero.");
                    return;
                }
                else
                {
                    bet = betAmount;
                }

                if (playerBalance < bet)
                {
                    await ctx.RespondAsync($"⚠️ <@{userId}>, you don't have enough coins to bet that amount. Your current balance is **{playerBalance:N0}** coins.");
                    return;
                }

                // Add user to lottery
                ulong guildId = ctx.Guild.Id;
                ulong channelId = ctx.Channel.Id;
                await db.AddToLotteryAsync(userId, bet, guildId, channelId);
                Console.WriteLine($"User {userId} entered the lottery with {bet:N0} coins.");

                // Update balance
                await db.UpdateBalanceAsync(userId, playerBalance - bet);
                Console.WriteLine("Balance updated.");
            }

            // Get total amount in lottery
            var entries = await db.GetLotteryEntriesAsync();
            Console.WriteLine($"Entries Count: {entries.Count}");
            var totalAmount = await db.GetTotalLotteryAmountAsync();
            Console.WriteLine($"Total Amount in Lottery: {totalAmount:N0}");

            // Prepare message
            if (entries.Count == 0)
            {
                await ctx.RespondAsync("⚠️ There are no participants in the lottery yet.");
                return;
            }

            // Message to show participants
            string participantsMessage = $"🎉 Current Lottery Participants **({entries.Count})**:\n";
            foreach (var entry in entries)
            {
                Console.WriteLine($"UserId: {entry.UserId}, Username: {entry.Username}, Amount: {entry.Amount:N0}");
                double chance = (double)entry.Amount / totalAmount * 100;
                participantsMessage += $"- **{entry.Username}**: {entry.Amount:N0} coins | **{chance:F2}%** chance\n";
            }

            var timeRemaining = GetTimeRemainingUntilLottery();
            participantsMessage += $"Total Lottery Pool: **{totalAmount:N0}** coins. ({timeRemaining} remaining)";

            // Send message
            await ctx.RespondAsync(participantsMessage);
            Console.WriteLine("Participants message sent.");
        }

        [Command("daily")]
        public async Task Daily(CommandContext ctx)
        {

            if (!await CheckCooldown(ctx, "daily")) return; // command cooldown

            ulong userId = ctx.User.Id;
            string username = ctx.User.Username;
            Database db = new Database();

            if (!await db.PlayerExistsAsync(userId))
            {
                await db.AddPlayerAsync(userId, username);
            }

            // set timezone to CET
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            DateTime nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            DateTime todayLocal = nowLocal.Date;

            DateTime? lastClaimed = await db.GetLastClaimedAsync(userId);
            int streak = await db.GetDailyStreakAsync(userId);

            if (!lastClaimed.HasValue || lastClaimed.Value.Date < todayLocal)
            {
                // if the user claimed yesterday, increment the streak
                if (lastClaimed.HasValue && (todayLocal - lastClaimed.Value.Date).Days == 1)
                {
                    streak++;
                }
                else
                {
                    streak = 1; // streak reset
                }

                Random random = new Random();
                long baseReward = random.Next(500, 1501); // random reward between 500 and 1500
                double bonusMultiplier = 1 + (streak * 0.05); // 5% bonus for each day of streak
                long bonus = (long)(baseReward * (streak * 0.1)); // bonus reward
                long totalReward = baseReward + bonus; // total reward

                long currentBalance = await db.GetBalanceAsync(userId);
                long newBalance = currentBalance + totalReward;

                await db.UpdateBalanceAsync(userId, newBalance);
                await db.UpdateLastClaimedAsync(userId);
                await db.UpdateDailyStreakAsync(userId, streak);

                // detail message
                await ctx.RespondAsync($@"🎉 **{ctx.User.Username}, you claimed your daily reward!**  
💰 **Base Reward:** {baseReward:N0} coins  
🔥 **Streak Bonus ({streak} days):** {bonus:N0} coins  
📈 **Total Reward:** {totalReward:N0} coins  
🏦 **New Balance:** {newBalance:N0} coins
");

                Console.WriteLine($"User {userId}: {username} claimed {totalReward:N0} coins (streak: {streak}, base: {baseReward}, bonus: {bonus}).");
            }
            else
            {
                // Výpočet zbývajícího času do půlnoci CET
                DateTime nextMidnight = todayLocal.AddDays(1);
                TimeSpan remainingTime = nextMidnight - nowLocal;
                string formattedTime = $"{remainingTime.Hours:D2}:{remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";

                await ctx.RespondAsync($"⚠️ {ctx.User.Username}, you can claim your daily reward again tomorrow at midnight! Remaining time: **{formattedTime}**.\nYour daily streak is **{streak} days** 🔥");
            }
        }

        [Command("work")]
        public async Task Work(CommandContext ctx)
        {

            if (!await CheckCooldown(ctx, "work")) return; // command cooldown

            ulong userId = ctx.User.Id;
            string username = ctx.User.Username;
            Database db = new Database();
            long currentBalance = await db.GetBalanceAsync(userId);

            if (!await db.PlayerExistsAsync(userId))
            {
                await db.AddPlayerAsync(userId, username);
            }

            if (currentBalance == 0)
            {
                Random random = new Random();
                long reward = random.Next(20, 101); // Reward between 20 and 100

                long newBalance = currentBalance + reward;
                await db.UpdateBalanceAsync(userId, newBalance);

                await ctx.RespondAsync($"💼 **{ctx.User.Username}**, you worked hard and earned **{reward:N0}** coins! ✨ Your new balance is: **{newBalance:N0}** coins. 🏅");
                Console.WriteLine("User {userId}: {username} worked and earned {reward} coins.");
            }
            else
            {
                await ctx.RespondAsync($"⚠️ **{ctx.User.Username}**, you can't work if you already have money. 💰 But don't worry, you can still try your luck in the casino with your **{currentBalance}** coins! 🎰");
            }
        }

        [Command("cf")]
        public async Task Coinflip(CommandContext ctx, string betAmountString)
        {

            if (!await CheckCooldown(ctx, "cf")) return; // command cooldown

            ulong userId = ctx.User.Id;
            string username = ctx.User.Username;
            Database db = new Database();
            long playerBalance = await db.GetBalanceAsync(userId);
            long betAmount;

            // check if player exists
            bool playerExists = await db.PlayerExistsAsync(userId);
            if (!playerExists)
            {
                await db.AddPlayerAsync(userId, username);
            }

            // check if bet amount is valid
            if (betAmountString == "all")
            {
                betAmount = playerBalance;
            }
            else if (!long.TryParse(betAmountString, out betAmount) || betAmount <= 0)
            {
                await ctx.RespondAsync("⚠️ Please enter a valid bet amount greater than zero.");
                return;
            }

            // check if player has enough balance
            if (playerBalance < betAmount || playerBalance == 0)
            {
                await ctx.RespondAsync($"⚠️ {username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance:N0}** coins.");
                return;
            }

            // choose heads or tails
            string betChoice = "heads";
            if (betChoice != "heads" && betChoice != "tails")
            {
                await ctx.RespondAsync("⚠️ Please choose between **heads** or **tails**.");
                return;
            }

            // flip the coin
            Random rand = new Random();
            string coinResult = rand.Next(0, 2) == 0 ? "heads" : "tails";

            // prepare result message
            string resultMessage = $"🪙 You bet on **{betChoice}**. The coin landed on **{coinResult}**! ";

            if (betChoice == coinResult)
            {
                //  player wins
                long winnings = betAmount; // double the bet amount
                playerBalance += winnings;
                resultMessage += $"🎉 You win! You received **{winnings:N0}** coins.";
            }
            else
            {
                // player loses
                playerBalance -= betAmount;
                resultMessage += $"❌ You lose! You lost **{betAmount:N0}** coins.";
            }

            // update player balance
            await db.UpdateBalanceAsync(userId, playerBalance);
            resultMessage += $" Your new balance is: **{playerBalance:N0}** coins.";

            // send result message
            await ctx.RespondAsync(resultMessage);
            Console.WriteLine($"User {userId}: {username} played coinflip with {betAmount:N0} coins");
        }

        [Command("rps")]
        public async Task RPS(CommandContext ctx, string playerChoice, string betString)
        {
            if (!await CheckCooldown(ctx, "rps")) return; // command cooldown

            ulong userId = ctx.User.Id;
            string username = ctx.User.Username;
            Database db = new Database();
            long bet;
            long playerBalance = await db.GetBalanceAsync(userId);

            if (!await db.PlayerExistsAsync(userId))
            {
                await db.AddPlayerAsync(userId, username);
            }

            if (betString == "all")
            {
                bet = playerBalance;
            }
            else if (!long.TryParse(betString, out long bett) || bett <= 0)
            {
                await ctx.RespondAsync("⚠️ Please enter a valid bet amount greater than zero.");
                return;
            }
            else
            {
                bet = bett;
            }

            if (playerBalance < bet)
            {
                await ctx.RespondAsync($"⚠️ {ctx.User.Username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance:N0}** coins.");
                return;
            }

            string[] validChoices = { "rock", "paper", "scissors" };

            if (!Array.Exists(validChoices, choice => choice == playerChoice.ToLower()))
            {
                await ctx.RespondAsync("⚠️ Please enter a valid choice: rock, paper, or scissors.");
                return;
            }

            Random random = new Random();
            int computerChoiceIndex = random.Next(0, 3);
            string computerChoice = computerChoiceIndex switch
            {
                0 => "rock",
                1 => "paper",
                2 => "scissors",
                _ => throw new InvalidOperationException()
            };

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
                playerBalance += bet;
            }
            else
            {
                result = "**Computer wins!**";
                playerBalance -= bet;
            }

            await db.UpdateBalanceAsync(userId, playerBalance);

            string message = $"You chose: **{playerChoice.ToLower()}** with **{bet:N0}** coins bet\n" +
                             $"Computer chose: **{computerChoice}**\n" +
                             $"{result}\n" +
                             $"Your new balance is: **{playerBalance:N0}** coins.";
            await ctx.RespondAsync(message);
            Console.WriteLine($"User {userId}: {username} played rock-paper-scissors with {bet:N0} coins");
        }

        [Command("leaderboard")]
        public async Task Leaderboard(CommandContext ctx)
        {

            if (!await CheckCooldown(ctx, "leaderboard")) return; // command cooldown

            ulong userId = ctx.User.Id;
            string username = ctx.User.Username;
            Database db = new Database();
            var topPlayers = await db.GetTopPlayersAsync();

            if (!await db.PlayerExistsAsync(userId))
            {
                await db.AddPlayerAsync(userId, username);
            }

            if (topPlayers.Count == 0)
            {
                await ctx.RespondAsync("⚠️ There are no players with a balance yet.");
                return;
            }

            string leaderboardMessage = "🏆 **Leaderboard - Top 10 Richest Players** 🏆\n";
            int rank = 1;
            foreach (var player in topPlayers)
            {
                leaderboardMessage += $"{rank}. **{player.Username}**: {player.Balance:N0} coins\n";
                rank++;
                if (rank > 10) break;
            }

            await ctx.RespondAsync(leaderboardMessage);
            Console.WriteLine($"User {userId}: {username} checked the leaderboard.");
        }

        [Command("removeplayer")]
        public async Task RemovePlayer(CommandContext ctx, ulong playerId)
        {
            if (ctx.User.Id != adminId)
            {
                await ctx.RespondAsync("⚠️ You do not have permission to perform this action.");
                return;
            }

            Database db = new Database();

            bool playerExists = await db.PlayerExistsAsync(playerId);
            if (!playerExists)
            {
                await ctx.RespondAsync($"⚠️ Player with ID **{playerId}** does not exist in the database.");
                return;
            }

            await db.RemovePlayerAsync(playerId);

            await ctx.RespondAsync($"✅ Player with ID **{playerId}** has been successfully removed from the database.");
            Console.WriteLine($"Player with ID {playerId} has been removed from the database.");
        }

        [Command("roulette")]
        public async Task Roulette(CommandContext ctx, string betType, string betAmountString)
        {
            if (!await CheckCooldown(ctx, "roulette")) return; // command cooldown

            ulong userId = ctx.User.Id;
            string username = ctx.User.Username;
            Database db = new Database();
            long playerBalance = await db.GetBalanceAsync(userId);
            long betAmount;

            bool playerExists = await db.PlayerExistsAsync(userId);
            if (!playerExists)
            {
                await db.AddPlayerAsync(userId, username);
            }

            if (betAmountString == "all")
            {
                betAmount = playerBalance;
            }
            else if (!long.TryParse(betAmountString, out betAmount) || betAmount <= 0)
            {
                await ctx.RespondAsync("⚠️ Please enter a valid bet amount greater than zero.");
                return;
            }

            if (playerBalance < betAmount || playerBalance == 0)
            {
                await ctx.RespondAsync($"⚠️ {ctx.User.Username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance:N0}** coins.");
                return;
            }

            var colors = new Dictionary<int, string>()
            {
                {0, "green"}, {1, "red"}, {2, "black"}, {3, "red"}, {4, "black"}, {5, "red"},
                {6, "black"}, {7, "red"}, {8, "black"}, {9, "red"}, {10, "black"}, {11, "black"},
                {12, "red"}, {13, "black"}, {14, "red"}, {15, "black"}, {16, "red"}, {17, "black"},
                {18, "red"}, {19, "red"}, {20, "black"}, {21, "red"}, {22, "black"}, {23, "red"},
                {24, "black"}, {25, "red"}, {26, "black"}, {27, "red"}, {28, "black"}, {29, "black"},
                {30, "red"}, {31, "black"}, {32, "red"}, {33, "black"}, {34, "red"}, {35, "black"},
                {36, "red"}
            };

            betType = betType.ToLower();
            bool isNumberBet = int.TryParse(betType, out int betNumber);

            if (!isNumberBet && !new[] { "red", "black", "green" }.Contains(betType))
            {
                await ctx.RespondAsync("❌ Invalid bet! You can bet on a **color (red/black/green)** or a **number (0-36)**.");
                return;
            }

            Random rand = new Random();
            int rolledNumber = rand.Next(37);
            string rolledColor = colors[rolledNumber];

            string resultMessage = $"🎡 The roulette wheel spins... **{rolledNumber}** :{rolledColor}_circle:! ";

            if (isNumberBet && betNumber == rolledNumber)
            {
                long winnings = betAmount * 35;
                playerBalance += winnings;
                resultMessage += $"🎉 **Jackpot!** You win **{winnings:NO}💰**!";
            }
            else if (!isNumberBet && betType == rolledColor)
            {
                long winnings = (betType == "green") ? betAmount * 35 : betAmount * 2;
                playerBalance += winnings;
                resultMessage += $"✅ You win **{winnings:NO}💰**!";
            }
            else
            {
                playerBalance -= betAmount;
                resultMessage += "❌ You lose!";
            }

            await db.UpdateBalanceAsync(userId, playerBalance);
            resultMessage += $" Your new balance: **{playerBalance}💰**.";

            await ctx.RespondAsync(resultMessage);
            Console.WriteLine($"User {userId}: {username} played roulette with {betAmount:N0} coins");

        }




    }
}
