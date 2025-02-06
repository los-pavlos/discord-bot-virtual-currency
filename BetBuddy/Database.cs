using System.Data.SQLite;

public class Database
{
    private string connectionString = "Data Source=players.db;Version=3;"; // Cesta k SQLite souboru

    public Database()
    {
        // Zkontroluje, zda existuje tabulka, pokud ne, vytvoří ji
        CreateTable();
    }

    // Metoda pro vytvoření tabulky, pokud neexistuje
    private void CreateTable()
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            connection.Open();
            var command = new SQLiteCommand(
                @"CREATE TABLE IF NOT EXISTS Playerss (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT,
                    Balance INTEGER,
                    LastClaimed DATETIME)",
                connection);
            command.ExecuteNonQuery();
        }

        //Console.WriteLine("Table 'Playerss' is created or already exists.");
    }


    // Metoda pro přidání hráče do databáze
    public async Task AddPlayerAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("INSERT INTO Playerss (Username, Balance, LastClaimed) VALUES (@username, 100, NULL)", connection);
            command.Parameters.AddWithValue("@username", username);
            await command.ExecuteNonQueryAsync();
        }
    }
    // Metoda pro získání rovnováhy hráče
    public async Task<int> GetBalanceAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT Balance FROM Playerss WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@username", username);
            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }
    }

    // Metoda pro aktualizaci měny hráče
    public async Task UpdateBalanceAsync(string username, int newBalance)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("UPDATE Playerss SET Balance = @balance WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@balance", newBalance);
            command.Parameters.AddWithValue("@username", username);
            await command.ExecuteNonQueryAsync();
        }
    }

    // Metoda pro zjištění, zda hráč existuje
    public async Task<bool> PlayerExistsAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT COUNT(1) FROM Playerss WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@username", username);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
    }
    // Metoda pro získání data posledního nároku
    public async Task<DateTime?> GetLastClaimedAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("SELECT LastClaimed FROM Playerss WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@username", username);
            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(result);
        }
    }

    // Metoda pro aktualizaci data posledního nároku
    public async Task UpdateLastClaimedAsync(string username)
    {
        using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var command = new SQLiteCommand("UPDATE Playerss SET LastClaimed = @lastClaimed WHERE Username = @username", connection);
            command.Parameters.AddWithValue("@lastClaimed", DateTime.UtcNow);
            command.Parameters.AddWithValue("@username", username);
            await command.ExecuteNonQueryAsync();
        }
    }
}
