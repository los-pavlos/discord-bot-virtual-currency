# Discord Bot - Virtual Currency Games

This is a Discord bot that allows users to play games and earn virtual currency within a server. Users can check their balance, participate in the lottery, claim daily rewards, work for coins, and enjoy classic games like rock-paper-scissors and coin flip. The bot utilizes SQLite to store user data.

## Features
#### Economics
- **bb daily**: Claim your daily reward (earn coins).
- **bb work**: Work to earn coins (random reward).
- **bb money**: Check your balance.

#### Gamble
- **bb cf `<bet>`**: Play coin flip with a specified bet.
- **bb lottery `[amount]`**: Enter the lottery or view current participants. (drawn daily)
- **bb rps `<choice>` `<bet>`**: Play rock-paper-scissors (choose rock, paper, or scissors and place a bet).

#### Admin commands
- **bb addmoney `<player>` `<amount>`**: Add money to a player's balance.
  
## Installation

### Install the Required Packages

Install the necessary dependencies using the following commands in your project directory:

```bash
dotnet add package DSharpPlus
dotnet add package DSharpPlus.CommandsNext
dotnet add package dotenv.net
dotnet add package Microsoft.Data.Sqlite
dotnet add package Newtonsoft.Json
```

### Setting Up the Bot

Create a .env file in the root of the project directory.

In the .env file, add your Discord bot token:

``
DISCORD_TOKEN=your_discord_token_here
``
Replace your_discord_token_here with your actual Discord bot token.

Make sure your bot has the necessary permissions on your Discord server (sending messages, reading message history, etc.).

### Running the Bot
After you’ve set up your .env file and installed the dependencies, you can run the bot using:

```bash
dotnet run
``` 
This will start the bot and it will connect to Discord. The bot should now be online and ready to interact with users in your Discord server.

### Database
The bot uses SQLite to store user balances, lottery entries, and other game-related data. The database file (players.db) will be automatically created in the project directory. If the database does not exist, it will be created upon running the bot for the first time.
