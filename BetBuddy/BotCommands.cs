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

namespace BetBuddy
{
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
                Color = DiscordColor.Blue
            }
            .AddField("💰 Economy", "`bb money` - Check your balance\n`bb daily` - Claim your daily reward\n`bb work` - Claim your work reward\n`bb leaderboard` - Check top 10 richest players")
            .AddField("🎮 Games", "`bb rps <choice> <bet>` - Rock Paper Scissors\n`bb cf <bet>` - Coin Flip\n`bb roulette <choice> <bet>` - Roulette")
            .AddField("🎟 Lottery", "`bb lottery <amount>` - Join the lottery (drawn daily)");

            await ctx.RespondAsync(embed);
        }

        [Command("money")]
        public async Task Money(CommandContext ctx)
        {
            ulong userId = ctx.User.Id;
            Database db = new Database();
            long balance = await db.GetBalanceAsync(userId);
            await ctx.RespondAsync($"💰 <@{userId}>, your current balance is: **{balance}** coins.");
            Console.WriteLine($"User {userId} checked their balance.");
        }


        [Command("give")]
        public async Task Give(CommandContext ctx, string playerMention, long amount)
        {
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
                await ctx.RespondAsync($"⚠️ You don't have enough money. Your curent balance is {balance}");
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
            await ctx.RespondAsync($"✅ **{amount}** coins have been sent to {mention}.\nNew balance: **{newBalance}** coins.");
        }


        [Command("addmoney")]
        public async Task AddMoney(CommandContext ctx, string playerMention, long amount)
        {
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
            await ctx.RespondAsync($"✅ **{amount}** coins have been added to {mention}'s balance.\nNew balance: **{newBalance}** coins.");
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
                    await ctx.RespondAsync($"⚠️ <@{userId}>, you don't have enough coins to bet that amount. Your current balance is **{playerBalance}** coins.");
                    return;
                }

                // Add user to lottery
                ulong guildId = ctx.Guild.Id;
                ulong channelId = ctx.Channel.Id;
                await db.AddToLotteryAsync(userId, bet, guildId, channelId);
                Console.WriteLine($"User {userId} entered the lottery with {bet} coins.");

                // Update balance
                await db.UpdateBalanceAsync(userId, playerBalance - bet);
                Console.WriteLine("Balance updated.");
            }

            // Get total amount in lottery
            var entries = await db.GetLotteryEntriesAsync();
            Console.WriteLine($"Entries Count: {entries.Count}");
            var totalAmount = await db.GetTotalLotteryAmountAsync();
            Console.WriteLine($"Total Amount in Lottery: {totalAmount}");

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
                Console.WriteLine($"UserId: {entry.UserId}, Username: {entry.Username}, Amount: {entry.Amount}");
                double chance = (double)entry.Amount / totalAmount * 100;
                participantsMessage += $"- **{entry.Username}**: {entry.Amount} coins | **{chance:F2}%** chance\n";
            }

            var timeRemaining = GetTimeRemainingUntilLottery();
            participantsMessage += $"Total Lottery Pool: **{totalAmount}** coins. ({timeRemaining} remaining)";

            // Send message
            await ctx.RespondAsync(participantsMessage);
            Console.WriteLine("Participants message sent.");
        }

        [Command("daily")]
        public async Task Daily(CommandContext ctx)
        {
            ulong userId = ctx.User.Id;  // Get Discord User ID
            string username = ctx.User.Username;  // Get Discord Username
            Database db = new Database();

            if (!await db.PlayerExistsAsync(userId))
            {
                await db.AddPlayerAsync(userId, username); // Use userId instead of username
            }

            DateTime? lastClaimed = await db.GetLastClaimedAsync(userId);
            DateTime today = DateTime.UtcNow.Date;

            if (!lastClaimed.HasValue || lastClaimed.Value.Date < today)
            {
                Random random = new Random();
                long reward = random.Next(500, 1501); // Reward between 500 and 1500
                long currentBalance = await db.GetBalanceAsync(userId);
                long newBalance = currentBalance + reward;

                await db.UpdateBalanceAsync(userId, newBalance);
                await db.UpdateLastClaimedAsync(userId);

                await ctx.RespondAsync($"🎉 {ctx.User.Username}, you claimed your daily reward of **{reward}** coins! New balance: **{newBalance}** coins.");
                Console.WriteLine($"User {userId}: {username} claimed daily reward of {reward} coins.");
            }
            else
            {
                var remainingTime = today.AddDays(1) - DateTime.UtcNow;
                await ctx.RespondAsync($"⚠️ {ctx.User.Username}, you can claim your daily reward again tomorrow at midnight! Remaining time: **{remainingTime:hh\\:mm\\:ss}**.");
            }
        }

        [Command("work")]
        public async Task Work(CommandContext ctx)
        {
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

                await ctx.RespondAsync($"💼 **{ctx.User.Username}**, you worked hard and earned **{reward}** coins! ✨ Your new balance is: **{newBalance}** coins. 🏅");
                Console.WriteLine("User {userId}: {username} worked and earned {reward} coins.");
            }
            else
            {
                await ctx.RespondAsync($"⚠️ **{ctx.User.Username}**, you can't work if you already have money. 💰 But don't worry, you can still try your luck in the casino! 🎰");
            }
        }

        [Command("cf")]
        public async Task Coinflip(CommandContext ctx, string betAmountString)
        {
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
                await ctx.RespondAsync($"⚠️ {username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance}** coins.");
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
                long winnings = betAmount * 2; // double the bet amount
                playerBalance += winnings;
                resultMessage += $"🎉 You win! You received **{winnings}** coins.";
            }
            else
            {
                // player loses
                playerBalance -= betAmount;
                resultMessage += $"❌ You lose! You lost **{betAmount}** coins.";
            }

            // update player balance
            await db.UpdateBalanceAsync(userId, playerBalance);
            resultMessage += $" Your new balance is: **{playerBalance}** coins.";

            // send result message
            await ctx.RespondAsync(resultMessage);
            Console.WriteLine($"User {userId}: {username} played coinflip with {betAmount} coins");
        }

        [Command("rps")]
        public async Task RPS(CommandContext ctx, string playerChoice, string betString)
        {
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
                await ctx.RespondAsync($"⚠️ {ctx.User.Username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance}** coins.");
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

            string message = $"You chose: **{playerChoice.ToLower()}** with **{bet}** coins bet\n" +
                             $"Computer chose: **{computerChoice}**\n" +
                             $"{result}\n" +
                             $"Your new balance is: **{playerBalance}** coins.";
            await ctx.RespondAsync(message);
            Console.WriteLine($"User {userId}: {username} played rock-paper-scissors with {bet} coins");
        }

        [Command("leaderboard")]
        public async Task Leaderboard(CommandContext ctx)
        {
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
                leaderboardMessage += $"{rank}. **{player.Username}**: {player.Balance} coins\n";
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
                await ctx.RespondAsync($"⚠️ {ctx.User.Username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance}** coins.");
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

            await db.UpdateBalanceAsync(userId, playerBalance);
            resultMessage += $" Your new balance: **{playerBalance}💰**.";

            await ctx.RespondAsync(resultMessage);
            Console.WriteLine($"User {userId}: {username} played roulette with {betAmount} coins");

        }

    }
}
