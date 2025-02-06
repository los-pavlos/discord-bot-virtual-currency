using System.Data.SQLite;
using System.Threading.Tasks;
using System;

public class Database
{
    private string connectionString = "Data Source=players.db;Version=3;";

    public Database()
    {
        CreateTables();
    }

    private void CreateTables()
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            connection.Open();

            // Create Players table if it doesn't exist
            var playersTableCommand = new SQLiteCommand(
                @"CREATE TABLE IF NOT EXISTS Players (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT,
                    Balance BIGINT,
                    LastClaimed DATETIME)",
                connection);
            playersTableCommand.ExecuteNonQuery();

            // Create LotteryEntries table if it doesn't exist
            var lotteryTableCommand = new SQLiteCommand(
                @"CREATE TABLE IF NOT EXISTS LotteryEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT,
                    Amount BIGINT,
                    EntryTime DATETIME)",
                connection);
            lotteryTableCommand.ExecuteNonQuery();
        }
    }

    public async Task<bool> PlayerExistsAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT COUNT(1) FROM Players WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@username", username);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
    }

    public async Task AddPlayerAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("INSERT INTO Players (Username, Balance, LastClaimed) VALUES (@username, 100, NULL)", connection);
            command.Parameters.AddWithValue("@username", username);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<long> GetBalanceAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT Balance FROM Players WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@username", username);
            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }
    }

    public async Task UpdateBalanceAsync(string username, long newBalance)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("UPDATE Players SET Balance = @balance WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@balance", newBalance);
            command.Parameters.AddWithValue("@username", username);
            await command.ExecuteNonQueryAsync();
        }
    }





    // Clean up old lottery entries
    public async Task DeleteOldLotteryEntriesAsync(DateTime cutoffTime)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("DELETE FROM LotteryEntries WHERE EntryTime < @cutoffTime", connection);
            command.Parameters.AddWithValue("@cutoffTime", cutoffTime);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<DateTime?> GetLastClaimedAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT LastClaimed FROM Players WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@username", username);
            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(result);
        }
    }

    public async Task UpdateLastClaimedAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("UPDATE Players SET LastClaimed = @lastClaimed WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@lastClaimed", DateTime.UtcNow);
            command.Parameters.AddWithValue("@username", username);
            await command.ExecuteNonQueryAsync();
        }
    }




    // Add a new entry or update the existing one if the player already has an entry
    public async Task AddLotteryEntryAsync(string username, long amount)
    {
        try
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                await connection.OpenAsync();

                // First, check if the user already has an entry
                var checkEntryCommand = new SQLiteCommand("SELECT COUNT(*) FROM LotteryEntries WHERE Username = @username", connection);
                checkEntryCommand.Parameters.AddWithValue("@username", username);
                var existingUserCount = Convert.ToInt32(await checkEntryCommand.ExecuteScalarAsync());

                string sqlQuery;

                if (existingUserCount == 0)
                {
                    // No entry found, insert a new entry
                    sqlQuery = "INSERT INTO LotteryEntries (Username, Amount, EntryTime) VALUES (@username, @amount, @entryTime)";
                }
                else
                {
                    // User exists, update their entry
                    sqlQuery = "UPDATE LotteryEntries SET Amount = Amount + @amount WHERE Username = @username";
                }

                // Execute the correct query (insert or update)
                var command = new SQLiteCommand(sqlQuery, connection);
                command.Parameters.AddWithValue("@username", username);
                command.Parameters.AddWithValue("@amount", amount);

                if (existingUserCount == 0)
                {
                    // Add entry time if it's a new insert
                    command.Parameters.AddWithValue("@entryTime", DateTime.UtcNow);
                }

                Console.WriteLine("Executing SQL: " + command.CommandText);
                int rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    Console.WriteLine($"Successfully added/updated entry for {username} in the lottery.");
                }
                else
                {
                    Console.WriteLine($"Failed to add/update entry for {username} in the lottery. No rows affected.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }
    }



    // Get all lottery entries
    public async Task<List<(string Username, long Amount)>> GetLotteryEntriesAsync()
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT Username, Amount FROM LotteryEntries", connection);
            var reader = await command.ExecuteReaderAsync();

            var entries = new List<(string Username, long Amount)>();
            while (await reader.ReadAsync())
            {
                entries.Add((reader.GetString(0), reader.GetInt64(1)));
            }

            return entries;
        }
    }

    // Get total amount of all lottery entries
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

    public async Task<List<(string Username, long Balance)>> GetTopPlayersAsync()
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT Username, Balance FROM Players ORDER BY Balance DESC LIMIT 10", connection);
            var reader = await command.ExecuteReaderAsync();

            var players = new List<(string Username, long Balance)>();
            while (await reader.ReadAsync())
            {
                players.Add((reader.GetString(0), reader.GetInt64(1)));
            }

            return players;
        }
    }

    public async Task RemovePlayerAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();

            // Vykonáme SQL dotaz pro odstranění hráče
            var command = new SQLiteCommand("DELETE FROM Players WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@username", username);

            int rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                Console.WriteLine($"Successfully removed player {username} from the database.");
            }
            else
            {
                Console.WriteLine($"No player found with username {username}.");
            }
        }
    }
}
