using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Newtonsoft.Json.Linq;
using System;
using System.Data.SQLite;
using System.Threading.Tasks;
using System;
using System.IO;
using dotenv.net;


namespace ForexCastBot
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            /*
             * 1. Přidejte NuGet balíček DSharpPlus
             * 2. Přidejte NuGet balíček DSharpPlus.CommandsNext
             * 3. Přidejte NuGet balíček Newtonsoft.Json
             * 4. Přidejte NuGet balíček dotenv.net
             * 5. vytvořte soubor .env v kořenovém adresáři projektu a vložte svůj Discord token ve formátu DISCORD_TOKEN=your_token_here
             */

            // Explicitně načítáme .env a vyhodíme chybu, pokud se nepodaří
            DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: false));

            string token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("❌ ERROR: Discord token nebyl načten! Zkontroluj .env soubor.");
                return;
            }

            Console.WriteLine("✅ Token úspěšně načten: " + token.Substring(0, 5) + "*****");

            var discord = new DiscordClient(new DiscordConfiguration()
            {
                Token = token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.All
            });


            var commands = discord.UseCommandsNext(new CommandsNextConfiguration()
            {
                StringPrefixes = new[] { "bb " } // Bot reaacts for commands starting with "bb"
            });

            commands.RegisterCommands<BotCommands>();

            await discord.ConnectAsync();
            await Task.Delay(-1); // Keeps bot running
        }
    }


    public class BotCommands : BaseCommandModule
    {


        private static readonly HttpClient client = new HttpClient();

        //  Hello command
        [Command("hello")]
        public async Task Hello(CommandContext ctx)
        {
            Console.WriteLine(ctx.User.Username + " Hello");
            await ctx.RespondAsync($"Hi, {ctx.User.Username}! 👋");
        }

        //  Convert currencies command
        [Command("convert")]
        public async Task ConvertCurrency(CommandContext ctx, string amount, string from, string to)
        {
            try
            {
                // Check if the amount is a valid number
                if (!double.TryParse(amount, out double parsedAmount) || parsedAmount <= 0)
                {
                    await ctx.RespondAsync("⚠️ Please enter a valid amount (positive number). For example: `100`");
                    return;
                }

                // Fetch the list of supported currencies dynamically from the API
                string symbolsUrl = "https://api.exchangerate-api.com/v4/latest/EUR"; // Base currency
                var symbolsResponse = await client.GetStringAsync(symbolsUrl);
                var symbolsData = JObject.Parse(symbolsResponse);

                // Extract the supported currencies from the API response
                var supportedCurrencies = symbolsData["rates"].ToObject<Dictionary<string, object>>().Keys.ToList();

                // Check if the provided currencies are valid
                if (!supportedCurrencies.Contains(from.ToUpper()) || !supportedCurrencies.Contains(to.ToUpper()))
                {
                    await ctx.RespondAsync($"⚠️ Unknown currency. Please check the currency abbreviations. Supported currencies: {string.Join(", ", supportedCurrencies.Take(30))}...");
                    return;
                }

                // Public API without key to get exchange rate
                string url = $"https://api.exchangerate-api.com/v4/latest/{from.ToUpper()}";
                var response = await client.GetStringAsync(url);
                var data = JObject.Parse(response);

                // Load rate
                double rate = double.Parse(data["rates"][to.ToUpper()].ToString());
                double result = parsedAmount * rate;

                // Output the conversion result
                await ctx.RespondAsync($"💱 **{amount} {from.ToUpper()}** = **{result:F2} {to.ToUpper()}**");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Currency conversion error: {ex.Message}");
                await ctx.RespondAsync("⚠️ Error occurred while converting currency. Please try again later.");
            }
        }

        [Command("rps")]
        public async Task RPS(CommandContext ctx, string playerChoice, string betString)
        {
            // Získání jména uživatele
            string username = ctx.User.Username;
            Database db = new Database();

            int bet;

            // Získání aktuálního zůstatku uživatele z databáze
            int playerBalance = await db.GetBalanceAsync(username);

            if (betString == "all")
            {
                bet = playerBalance;
            }
            else if (!int.TryParse(betString, out int bett) || bett <= 0)    // Zkontroluj, zda je sázka platným číslem
            {
                await ctx.RespondAsync("⚠️ Please enter a valid bet amount greater than zero.");
                return;
            } else
            {
                // Převedení sázky na číslopřevod na string
                bet = int.Parse(betString);
            }

            Console.WriteLine(ctx.User.Username + " RPS");

            // Zkontroluj, zda hráč má dost peněz na sázení
            if (playerBalance < bet)
            {
                await ctx.RespondAsync($"⚠️ {username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance}** coins.");
                return;
            }

            // Seznam platných voleb pro hru
            string[] validChoices = { "rock", "paper", "scissors" };

            // Zkontroluj, zda hráč zadal platnou volbu
            if (!Array.Exists(validChoices, choice => choice == playerChoice.ToLower()))
            {
                await ctx.RespondAsync("⚠️ Please enter a valid choice: rock, paper, or scissors.");
                return;
            }

            // Výběr počítače
            Random random = new Random();
            int computerChoiceIndex = random.Next(0, 3);
            string computerChoice = computerChoiceIndex switch
            {
                0 => "rock",
                1 => "paper",
                2 => "scissors",
                _ => throw new InvalidOperationException()
            };

            // Určete vítěze
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
                playerBalance += bet; // Hráč vyhrál, přičteme bet k zůstatku
            }
            else
            {
                result = "**Computer wins!**";
                playerBalance -= bet; // Počítač vyhrál, odečteme bet od zůstatku
            }

            // Uložení nového zůstatku do databáze
            await db.UpdateBalanceAsync(username, playerBalance);

            // Odpověď s výsledkem
            string message = $"You chose: **{playerChoice.ToLower()}** with **{bet}** coins bet\n" +
                             $"Computer chose: **{computerChoice}**\n" +
                             $"{result}\n" +
                             $"Your new balance is: **{playerBalance}** coins.";
            await ctx.RespondAsync(message);
        }

        [Command("addmoney")]
        public async Task AddMoney(CommandContext ctx, string playerUsername, int amount)
        {
            if (amount <= 0)
            {
                await ctx.RespondAsync("⚠️ Please enter a valid amount greater than zero.");
                return;
            }

            Database db = new Database();

            Console.WriteLine(ctx.User.Username + " addMoney");

            // Kontrola, zda hráč existuje
            bool playerExists = await db.PlayerExistsAsync(playerUsername);

            if (!playerExists)
            {
                await db.AddPlayerAsync(playerUsername); // Vytvoření nového hráče, pokud neexistuje
            }

            // Získáme aktuální zůstatek hráče
            int currentBalance = await db.GetBalanceAsync(playerUsername);

            // Přičteme nové peníze
            int newBalance = currentBalance + amount;

            // Uložíme nový zůstatek do databáze
            await db.UpdateBalanceAsync(playerUsername, newBalance);

            // Odpověď pro administrátora
            await ctx.RespondAsync($"✅ **{amount}** coins have been added to {playerUsername}'s balance.\n" +
                                    $"New balance: **{newBalance}** coins.");
        }

        [Command("money")]
        public async Task Money(CommandContext ctx)
        {
            string username = ctx.User.Username;

            Console.WriteLine(ctx.User.Username + " money");

            Database db = new Database();
            int balance = await db.GetBalanceAsync(username);
            await ctx.RespondAsync($"💰 {username}, your current balance is: **{balance}** coins.");
        }

        [Command("work")]
        public async Task work(CommandContext ctx)
        {
            string username = ctx.User.Username;

            Console.WriteLine(ctx.User.Username + " work");
            Random random = new Random();
            int workReward = random.Next(50, 200);
            Database db = new Database();
            int balance = await db.GetBalanceAsync(username);
            int newBalance = balance + workReward;
            await db.UpdateBalanceAsync(username, newBalance);
            await ctx.RespondAsync($"💰 I see you are working well {username}, there is your reward **{workReward}** coins. Your current balance is: **{newBalance}** coins.");
        }

        [Command("cf")]
        public async Task coinFlip(CommandContext ctx, string betString)
        {
            // Získání jména uživatele
            string username = ctx.User.Username;
            Database db = new Database();
          
         
            Console.WriteLine(ctx.User.Username + " CF");

            int bet;

            // Získání aktuálního zůstatku uživatele z databáze
            int playerBalance = await db.GetBalanceAsync(username);

            if (betString == "all")
            {
                bet = playerBalance;
            }
            else if (!int.TryParse(betString, out int bett) || bett <= 0)    // Zkontroluj, zda je sázka platným číslem
            {
                await ctx.RespondAsync("⚠️ Please enter a valid bet amount greater than zero.");
                return;
            }
            else
            {
                // Převedení sázky na číslopřevod na string
                bet = int.Parse(betString);
            }

            // Zkontroluje, zda hráč má dost peněz na sázení
            if (playerBalance < bet)
            {
                await ctx.RespondAsync($"⚠️ {username}, you don't have enough virtual currency to place that bet. Your current balance is **{playerBalance}** coins.");
                return;
            }

          
            // Výběr počítače
            Random random = new Random();
            int computerChoiceIndex = random.Next(0, 2);

            string result = "";
           
            if (computerChoiceIndex==0)
            {
                result = "**HEADS! You win!**";
                playerBalance += bet; // Hráč vyhrál, přičteme bet k zůstatku
            }
            else
            {
                result = "**TAILS! You lose!**";
                playerBalance -= bet; // Počítač vyhrál, odečteme bet od zůstatku
            }

            // Uložení nového zůstatku do databáze
            await db.UpdateBalanceAsync(username, playerBalance);

            // Odpověď s výsledkem
            string message = $"You spent **{bet}** coins on **HEADS**\n" +
                             $"{result}\n" +
                             $"Your new balance is: **{playerBalance}** coins.";
            await ctx.RespondAsync(message);
        }

        [Command("daily")]
        public async Task daily(CommandContext ctx)
        {
            string username = ctx.User.Username;
            Database db = new Database();

            Console.WriteLine(ctx.User.Username + " Daily");

            // Zkontroluj, zda hráč existuje v databázi, pokud ne, přidej ho
            if (!await db.PlayerExistsAsync(username))
            {
                await db.AddPlayerAsync(username);
                await ctx.RespondAsync($"👋 {username}, you have been registered! You start with **100** coins.");
            }

            // Získání posledního nároku
            DateTime? lastClaimed = await db.GetLastClaimedAsync(username);

            // Logování pro ladění
            Console.WriteLine($"LastClaimed for {username}: {lastClaimed}");

            // Zkontroluj, zda hráč již dnes nárokoval odměnu
            if (lastClaimed.HasValue && lastClaimed.Value.Date == DateTime.UtcNow.Date)
            {
                await ctx.RespondAsync("⚠️ You have already claimed your daily reward today. Please come back tomorrow.");
                return;
            }

            // Přidej hráči daily odměnu mincí
            Random random = new Random();
            int reward = random.Next(500, 1500);
            int playerBalance = await db.GetBalanceAsync(username);
            playerBalance += reward;
            await db.UpdateBalanceAsync(username, playerBalance);

            // Logování pro ladění
            Console.WriteLine($"New balance for {username}: {playerBalance}");

            // Aktualizuj datum posledního nároku
            await db.UpdateLastClaimedAsync(username);

            // Odpověď s potvrzením
            await ctx.RespondAsync($"✨ {username}, you have claimed your daily reward of **{reward}** coins! Your new balance is: **{playerBalance}** coins.");
        }


    }
}