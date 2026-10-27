namespace FlightTickets;

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Login { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public bool IsAdmin { get; set; }
}

public sealed class Flight
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "";
    public string Airline { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public DateTime Departure { get; set; }
    public DateTime Arrival { get; set; }
    public decimal Price { get; set; }
    public int Seats { get; set; }
}

public sealed class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FlightId { get; set; }
    public Guid UserId { get; set; }
    public string Passenger { get; set; } = "";
    public string Passport { get; set; } = "";
    public int Seat { get; set; }
    public decimal Price { get; set; }
    public DateTime Purchased { get; set; } = DateTime.Now;
    public bool Cancelled { get; set; }
}

public sealed class StoreData
{
    public List<Account> Users { get; set; } = new();
    public List<Flight> Flights { get; set; } = new();
    public List<Ticket> Tickets { get; set; } = new();
}
