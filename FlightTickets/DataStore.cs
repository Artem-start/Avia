using System.Security.Cryptography;
using System.Text.Json;

namespace FlightTickets;

public sealed class DataStore
{
    // Данные учебного приложения локальные: %LOCALAPPDATA%\FlightTicketsCourse\data.json
    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlightTicketsCourse", "data.json");

    public StoreData Data { get; private set; } = new();

    public DataStore()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                Data = JsonSerializer.Deserialize<StoreData>(File.ReadAllText(FilePath)) ?? new();
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("Файл данных повреждён. Проверьте: " + FilePath);
            }
        }
        if (!Data.Users.Any(u => u.IsAdmin))
            Data.Users.Add(NewAccount("admin", "Администратор", "Admin123!", true));
        if (Data.Flights.Count == 0 && !File.Exists(FilePath)) SeedFlights();
        Save();
    }

    public static Account NewAccount(string login, string name, string password, bool admin = false)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA256, 32);
        return new Account { Login = login, Name = name, Salt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(hash), IsAdmin = admin };
    }

    public Account? SignIn(string login, string password)
    {
        Account? user = Data.Users.FirstOrDefault(x => x.Login.Equals(login.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user == null) return null;
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(user.Salt),
            120_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(user.PasswordHash)) ? user : null;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, FilePath, true);
    }

    public int Available(Flight flight) => flight.Seats - Data.Tickets.Count(t => t.FlightId == flight.Id && !t.Cancelled);

    private void SeedFlights()
    {
        string[] cities = { "Москва", "Санкт-Петербург", "Казань", "Сочи", "Екатеринбург", "Новосибирск" };
        for (int day = 1; day <= 15; day++)
        {
            for (int i = 0; i < 6; i++)
            {
                DateTime departure = DateTime.Today.AddDays(day).AddHours(7 + i * 2);
                Data.Flights.Add(new Flight
                {
                    Number = $"FT{110 + i}", Airline = i % 2 == 0 ? "АэроЛинии" : "Небо",
                    From = cities[i], To = cities[(i + 1 + day % 3) % cities.Length],
                    Departure = departure, Arrival = departure.AddHours(2 + i % 3),
                    Price = 3900 + i * 1250 + day * 110, Seats = 24 + i * 6
                });
            }
        }
    }
}
