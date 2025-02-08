using System.Data.SQLite;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Data;

public class Database
{
    private string connectionString = "Data Source=players.db;Version=3;Journal Mode=WAL;Cache Size=5000;";


    public Database()
    {
        CreateTables();
    }

    private void CreateTables()
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            connection.Open();

            var playersTableCommand = new SQLiteCommand(
                @"CREATE TABLE IF NOT EXISTS Players (
                    UserId INTEGER PRIMARY KEY,
                    Username string,
                    Balance BIGINT,
                    LastClaimed DATETIME)",
                connection);
            playersTableCommand.ExecuteNonQuery();

            var lotteryTableCommand = new SQLiteCommand(
                @"CREATE TABLE IF NOT EXISTS LotteryEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    Amount BIGINT,
                    EntryTime DATETIME,
                    FOREIGN KEY (UserId) REFERENCES Players(UserId))",
                connection);
            lotteryTableCommand.ExecuteNonQuery();
        }
    }

    public async Task<bool> PlayerExistsAsync(ulong userId)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT COUNT(1) FROM Players WHERE UserId = @userId", connection);
            command.Parameters.AddWithValue("@userId", userId);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
    }
    public async Task<List<(string Username, long Balance)>> GetTopPlayersAsync()
    {
        var topPlayers = new List<(string Username, long Balance)>();

        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();

            // SQL query to get top 10 players by balance
            var query = "SELECT Username, Balance FROM Players ORDER BY Balance DESC LIMIT 10";

            using (var command = new SQLiteCommand(query, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var username = reader.GetString(0);
                        var balance = reader.GetInt64(1);
                        topPlayers.Add((username, balance));
                    }
                }
            }
        }

        return topPlayers;
    }

    public async Task AddPlayerAsync(ulong userId, string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("INSERT INTO Players (UserId, Username, Balance, LastClaimed) VALUES (@userId, @username, 100, NULL)", connection);
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@username", username);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<long> GetBalanceAsync(ulong userId)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT Balance FROM Players WHERE UserId = @userId", connection);
            command.Parameters.AddWithValue("@userId", userId);
            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }
    }

    public async Task UpdateBalanceAsync(ulong userId, long newBalance)
    {
        int maxRetries = 3;
        int attempt = 0;
        bool updateSuccess = false;

        while (attempt < maxRetries && !updateSuccess)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    await connection.OpenAsync();
                    Console.WriteLine($"Updating balance for UserId: {userId} to {newBalance}");

                    var command = new SQLiteCommand("UPDATE Players SET Balance = @balance WHERE UserId = @userId", connection);
                    command.Parameters.AddWithValue("@balance", newBalance);
                    command.Parameters.AddWithValue("@userId", userId);

                    var rowsAffected = await command.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        Console.WriteLine($"No rows were updated for UserId: {userId}. Check if user exists.");
                    }
                    else
                    {
                        Console.WriteLine($"Successfully updated balance for UserId: {userId} to {newBalance}");
                        updateSuccess = true; // Change the flag to true to break the loop
                    }
                }
            }
            catch (SQLiteException ex)
            {
                // If the exception message contains "database is locked", retry the operation
                if (ex.Message.Contains("database is locked"))
                {
                    attempt++;
                    Console.WriteLine($"Attempt {attempt}/{maxRetries} failed due to database being locked. Retrying in 1 second...");
                    await Task.Delay(1000); // wait for 1 second before retrying
                }
                else
                {
                    Console.WriteLine($"Error updating balance: {ex.Message}");
                    throw; // rethrow the exception if it's not a "database is locked" error
                }
            }
        }

        if (!updateSuccess)
        {
            Console.WriteLine($"Failed to update balance for UserId: {userId} after {maxRetries} attempts.");
        }
    }


    public async Task<DateTime?> GetLastClaimedAsync(ulong userId)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT LastClaimed FROM Players WHERE UserId = @userId", connection);
            command.Parameters.AddWithValue("@userId", userId);
            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(result);
        }
    }

    public async Task UpdateLastClaimedAsync(ulong userId)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("UPDATE Players SET LastClaimed = @lastClaimed WHERE UserId = @userId", connection);
            command.Parameters.AddWithValue("@lastClaimed", DateTime.UtcNow);
            command.Parameters.AddWithValue("@userId", userId);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task AddToLotteryAsync(ulong userId, long amount)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();

            // check if the user already has an entry in the lottery
            var checkCommand = new SQLiteCommand("SELECT Amount FROM LotteryEntries WHERE UserId = @UserId", connection);
            checkCommand.Parameters.AddWithValue("@UserId", userId);

            var reader = await checkCommand.ExecuteReaderAsync();

            if (await reader.ReadAsync()) // user already has an entry in the lottery
            {
                long currentAmount = reader.GetInt64(0);
                long newAmount = currentAmount + amount;

                // update the amount
                var updateCommand = new SQLiteCommand("UPDATE LotteryEntries SET Amount = @Amount WHERE UserId = @UserId", connection);
                updateCommand.Parameters.AddWithValue("@Amount", newAmount);
                updateCommand.Parameters.AddWithValue("@UserId", userId);

                await updateCommand.ExecuteNonQueryAsync();
            }
            else // user doesn't have an entry in the lottery
            {
                var insertCommand = new SQLiteCommand("INSERT INTO LotteryEntries (UserId, Amount) VALUES (@UserId, @Amount)", connection);
                insertCommand.Parameters.AddWithValue("@UserId", userId);
                insertCommand.Parameters.AddWithValue("@Amount", amount);

                await insertCommand.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task<List<(ulong UserId, string Username, long Amount)>> GetLotteryEntriesAsync()
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();

            
            var command = new SQLiteCommand("SELECT LotteryEntries.UserId, Players.Username, LotteryEntries.Amount FROM LotteryEntries JOIN Players ON LotteryEntries.UserId = Players.UserId", connection);

            var reader = await command.ExecuteReaderAsync();
            var entries = new List<(ulong UserId, string Username, long Amount)>();

            while (await reader.ReadAsync())
            {
                // Get the UserId, Username, and Amount from the query result
                ulong userId = (ulong)reader.GetInt64(0);
                string username = reader.GetString(1);
                long amount = reader.GetInt64(2);

                // Add the tuple to the list
                entries.Add((userId, username, amount));
            }

            return entries;
        }
    }



    public async Task RemovePlayerAsync(ulong userId)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("DELETE FROM Players WHERE UserId = @userId", connection);
            command.Parameters.AddWithValue("@userId", userId);
            await command.ExecuteNonQueryAsync();
        }
    }

    // method to get the total amount of the lottery
    public async Task<long> GetTotalLotteryAmountAsync()
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT SUM(Amount) FROM LotteryEntries", connection);
            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }
    }

    // method to delete all lottery entries older than a certain date
    public async Task DeleteOldLotteryEntriesAsync(DateTime cutoffDate)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("DELETE FROM LotteryEntries WHERE EntryTime < @cutoffDate", connection);
            command.Parameters.AddWithValue("@cutoffDate", cutoffDate);
            await command.ExecuteNonQueryAsync();
        }
    }
}
