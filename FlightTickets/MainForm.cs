using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;

namespace FlightTickets;

public sealed class MainForm : Form
{
    private readonly DataStore db;
    private Account? current;
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly Label who = new() { AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly Button authButton = Button("Вход / Регистрация");
    private readonly DataGridView flightGrid = Grid();
    private readonly DataGridView ticketGrid = Grid();
    private readonly DataGridView adminGrid = Grid();
    private readonly ComboBox from = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
    private readonly ComboBox to = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
    private readonly DateTimePicker day = new() { Format = DateTimePickerFormat.Short, Width = 130 };
    private readonly CheckBox anyDay = new() { Text = "Все даты", AutoSize = true };
    private readonly Label stats = new() { AutoSize = true, Font = new Font("Segoe UI", 10) };
    private readonly Label adminStats = new() { AutoSize = true, Font = new Font("Segoe UI", 10) };
    private readonly TabPage findTab = new("Поиск рейсов");
    private readonly TabPage myTab = new("Мои билеты");
    private readonly TabPage adminTab = new("Администратор");
    private List<Flight> shownFlights = new();
    private List<Ticket> shownTickets = new();
    private List<Flight> shownAdmin = new();

    public MainForm()
    {
        Text = "Авиабилеты — курсовой проект";
        Width = 1160; Height = 750; MinimumSize = new Size(900, 570);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.White;
        try { db = new DataStore(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }

        var header = new Panel { Height = 74, Dock = DockStyle.Top, BackColor = Color.FromArgb(17, 57, 107) };
        var heading = new Label { Text = "✈   АВИАБИЛЕТЫ", ForeColor = Color.White,
            Font = new Font("Segoe UI", 20, FontStyle.Bold), AutoSize = true, Left = 23, Top = 18 };
        who.Text = "Гость";
        who.Location = new Point(630, 27);
        who.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        authButton.Location = new Point(900, 19);
        authButton.Size = new Size(220, 39);
        authButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        authButton.Click += (_, _) => { if (current == null) LoginDialog(); else Logout(); };
        header.Controls.AddRange(new Control[] { heading, who, authButton });
        Controls.Add(tabs);
        Controls.Add(header);
        tabs.TabPages.AddRange(new[] { findTab, myTab });
        InitFlights(); InitTickets(); InitAdmin();
        day.Value = DateTime.Today.AddDays(1);
        UpdateCities(); RefreshAll();
    }

    private static Button Button(string text) => new()
    {
        Text = text, AutoSize = false, Size = new Size(155, 39),
        BackColor = Color.FromArgb(31, 115, 204), ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
    };

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false,
        BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
        RowTemplate = { Height = 35 }, ColumnHeadersHeight = 42,
        EnableHeadersVisualStyles = false, ColumnHeadersDefaultCellStyle = { BackColor = Color.FromArgb(225, 237, 251) }
    };

    private static FlowLayoutPanel Tools() => new()
    {
        Dock = DockStyle.Top, Height = 70, Padding = new Padding(14),
        WrapContents = false, AutoScroll = true, BackColor = Color.FromArgb(243, 247, 252)
    };
    private static Label Label(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(8, 12, 5, 0) };
    private static void AddButton(FlowLayoutPanel bar, string title, EventHandler action)
    {
        var button = Button(title);
        button.Margin = new Padding(7, 0, 3, 0);
        button.Click += action;
        bar.Controls.Add(button);
    }

    private void InitFlights()
    {
        var bar = Tools();
        bar.Controls.Add(Label("Откуда:")); bar.Controls.Add(from);
        bar.Controls.Add(Label("Куда:")); bar.Controls.Add(to);
        bar.Controls.Add(Label("Дата:")); bar.Controls.Add(day);
        bar.Controls.Add(anyDay);
        AddButton(bar, "Найти", (_, _) => RefreshFlights());
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 74, Padding = new Padding(20, 16, 20, 10) };
        var buy = Button("Купить билет"); buy.Width = 190;
        buy.Click += (_, _) => BuySelected();
        bottom.Controls.Add(buy);
        stats.Location = new Point(232, 26); bottom.Controls.Add(stats);
        findTab.Controls.Add(flightGrid); findTab.Controls.Add(bottom); findTab.Controls.Add(bar);
    }

    private void InitTickets()
    {
        var bar = Tools();
        AddButton(bar, "Обновить", (_, _) => RefreshTickets());
        AddButton(bar, "Показать билет", (_, _) => ShowTicket());
        AddButton(bar, "Отменить билет", (_, _) => CancelTicket());
        myTab.Controls.Add(ticketGrid); myTab.Controls.Add(bar);
    }

    private void InitAdmin()
    {
        var bar = Tools();
        AddButton(bar, "Добавить рейс", (_, _) => EditFlight(null));
        AddButton(bar, "Изменить рейс", (_, _) => { if (SelectedAdmin() is { } f) EditFlight(f); });
        AddButton(bar, "Удалить рейс", (_, _) => DeleteFlight());
        AddButton(bar, "Обновить", (_, _) => RefreshAdmin());
        var foot = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(15, 10, 10, 4) };
        adminStats.Dock = DockStyle.Fill; foot.Controls.Add(adminStats);
        adminTab.Controls.Add(adminGrid); adminTab.Controls.Add(foot); adminTab.Controls.Add(bar);
    }

    private void UpdateCities()
    {
        string a = from.Text, b = to.Text;
        string[] cities = db.Data.Flights.SelectMany(x => new[] { x.From, x.To }).Distinct().OrderBy(x => x).ToArray();
        from.Items.Clear(); to.Items.Clear();
        from.Items.Add("Все города"); to.Items.Add("Все города");
        from.Items.AddRange(cities); to.Items.AddRange(cities);
        from.SelectedItem = from.Items.Contains(a) ? a : "Все города";
        to.SelectedItem = to.Items.Contains(b) ? b : "Все города";
    }

    private void RefreshAll() { RefreshFlights(); RefreshTickets(); RefreshAdmin(); }
    private void RefreshFlights()
    {
        shownFlights = db.Data.Flights.Where(f =>
           f.Departure >= DateTime.Now &&
           (from.Text == "Все города" || f.From == from.Text) &&
           (to.Text == "Все города" || f.To == to.Text) &&
           (anyDay.Checked || f.Departure.Date == day.Value.Date))
           .OrderBy(f => f.Departure).ToList();
        flightGrid.DataSource = shownFlights.Select(f => new
        {
            Рейс = f.Number, Авиакомпания = f.Airline, Откуда = f.From, Куда = f.To,
            Вылет = f.Departure.ToString("dd.MM.yyyy HH:mm"),
            Прилёт = f.Arrival.ToString("dd.MM.yyyy HH:mm"),
            Цена = f.Price.ToString("N0") + " ₽", Мест = db.Available(f)
        }).ToList();
        stats.Text = $"Найдено рейсов: {shownFlights.Count}";
    }

    private void RefreshTickets()
    {
        shownTickets = current == null ? new() : db.Data.Tickets.Where(t => t.UserId == current.Id)
            .OrderByDescending(t => t.Purchased).ToList();
        ticketGrid.DataSource = shownTickets.Select(t =>
        {
            Flight? f = db.Data.Flights.FirstOrDefault(x => x.Id == t.FlightId);
            return new
            {
                Номер = t.Id.ToString("N")[..8].ToUpperInvariant(),
                Рейс = f?.Number ?? "—", Маршрут = f == null ? "—" : f.From + " → " + f.To,
                Вылет = f?.Departure.ToString("dd.MM.yyyy HH:mm") ?? "—",
                Пассажир = t.Passenger, Место = t.Seat,
                Стоимость = t.Price.ToString("N0") + " ₽",
                Статус = t.Cancelled ? "Отменён" : "Куплен"
            };
        }).ToList();
    }

    private void RefreshAdmin()
    {
        shownAdmin = db.Data.Flights.OrderBy(x => x.Departure).ToList();
        adminGrid.DataSource = shownAdmin.Select(f => new
        {
            Рейс = f.Number, Авиакомпания = f.Airline,
            Откуда = f.From, Куда = f.To,
            Вылет = f.Departure.ToString("dd.MM.yyyy HH:mm"),
            Прилёт = f.Arrival.ToString("dd.MM.yyyy HH:mm"),
            Цена = f.Price.ToString("N0") + " ₽", Всего = f.Seats, Свободно = db.Available(f)
        }).ToList();
        adminStats.Text = $"Рейсов: {db.Data.Flights.Count}   ·   Пользователей: {db.Data.Users.Count}   ·   Продано билетов: {db.Data.Tickets.Count(x => !x.Cancelled)}   ·   Выручка (учебная): {db.Data.Tickets.Where(x => !x.Cancelled).Sum(x => x.Price):N0} ₽";
    }

    private Flight? SelectedFlight() => flightGrid.CurrentRow?.Index is int i && i >= 0 && i < shownFlights.Count ? shownFlights[i] : null;
    private Flight? SelectedAdmin() => adminGrid.CurrentRow?.Index is int i && i >= 0 && i < shownAdmin.Count ? shownAdmin[i] : null;
    private Ticket? SelectedTicket() => ticketGrid.CurrentRow?.Index is int i && i >= 0 && i < shownTickets.Count ? shownTickets[i] : null;

    private void LoginDialog()
    {
        using var dlg = Dialog("Вход и регистрация", 430, 340);
        var login = Field(dlg, "Логин", 20, 20);
        var pass = Field(dlg, "Пароль", 20, 87); pass.UseSystemPasswordChar = true;
        var enter = Button("Войти"); enter.Location = new Point(20, 166);
        var register = Button("Регистрация"); register.Location = new Point(190, 166);
        var info = new Label { Text = "Демо-администратор: admin / Admin123!", AutoSize = true,
            Location = new Point(20, 236), ForeColor = Color.DimGray };
        dlg.Controls.AddRange(new Control[] { enter, register, info });
        enter.Click += (_, _) =>
        {
            Account? user = db.SignIn(login.Text, pass.Text);
            if (user == null) { Warn("Неверный логин или пароль."); return; }
            current = user; dlg.DialogResult = DialogResult.OK; dlg.Close();
        };
        register.Click += (_, _) => { dlg.Hide(); RegisterDialog(); dlg.Close(); };
        dlg.ShowDialog(this);
        UpdateUser();
    }

    private void RegisterDialog()
    {
        using var dlg = Dialog("Регистрация пассажира", 445, 385);
        var name = Field(dlg, "Фамилия и имя", 20, 18);
        var login = Field(dlg, "Логин", 20, 88);
        var pass = Field(dlg, "Пароль (от 6 символов)", 20, 158); pass.UseSystemPasswordChar = true;
        var create = Button("Создать аккаунт"); create.Location = new Point(20, 240); create.Width = 205;
        dlg.Controls.Add(create);
        create.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(login.Text) || pass.Text.Length < 6)
            { Warn("Укажите имя, логин и пароль не короче 6 символов."); return; }
            if (db.Data.Users.Any(x => x.Login.Equals(login.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
            { Warn("Такой логин уже существует."); return; }
            Account account = DataStore.NewAccount(login.Text.Trim(), name.Text.Trim(), pass.Text);
            db.Data.Users.Add(account); db.Save(); current = account;
            dlg.DialogResult = DialogResult.OK; dlg.Close();
        };
        dlg.ShowDialog(this);
        UpdateUser();
    }

    private void Logout() { current = null; UpdateUser(); }
    private void UpdateUser()
    {
        who.Text = current == null ? "Гость" : $"{current.Name} {(current.IsAdmin ? "(админ)" : "")}";
        authButton.Text = current == null ? "Вход / Регистрация" : "Выйти";
        if (current?.IsAdmin == true && !tabs.TabPages.Contains(adminTab)) tabs.TabPages.Add(adminTab);
        if (current?.IsAdmin != true && tabs.TabPages.Contains(adminTab)) tabs.TabPages.Remove(adminTab);
        RefreshAll();
    }

    private void BuySelected()
    {
        Flight? f = SelectedFlight();
        if (f == null) { Warn("Выберите рейс в таблице."); return; }
        if (current == null) { Warn("Для покупки билета войдите или зарегистрируйтесь."); LoginDialog(); return; }
        if (db.Available(f) <= 0) { Warn("На рейсе нет свободных мест."); return; }
        using var dlg = Dialog("Оформление авиабилета", 490, 395);
        var description = new Label { Text = $"Рейс {f.Number}: {f.From} → {f.To}\n{f.Departure:dd.MM.yyyy HH:mm}  ·  {f.Price:N0} ₽",
            Location = new Point(22, 16), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
        dlg.Controls.Add(description);
        var name = Field(dlg, "ФИО пассажира", 22, 94); name.Text = current.Name;
        var passport = Field(dlg, "Номер документа (паспорт)", 22, 160);
        var confirm = Button("Оплатить (демо)"); confirm.Location = new Point(22, 247); confirm.Width = 210;
        var note = new Label { Text = "Учебная оплата: реальные деньги не списываются.",
            Location = new Point(22, 310), AutoSize = true, ForeColor = Color.DimGray };
        dlg.Controls.AddRange(new Control[] { confirm, note });
        confirm.Click += (_, _) =>
        {
            if (name.Text.Trim().Length < 5 || passport.Text.Trim().Length < 5)
            { Warn("Укажите ФИО и номер документа (не меньше 5 символов). "); return; }
            if (db.Available(f) <= 0) { Warn("Все места заняты."); return; }
            var taken = db.Data.Tickets.Where(x => x.FlightId == f.Id && !x.Cancelled).Select(x => x.Seat).ToHashSet();
            int seat = Enumerable.Range(1, f.Seats).First(x => !taken.Contains(x));
            var ticket = new Ticket { UserId = current.Id, FlightId = f.Id,
                Passenger = name.Text.Trim(), Passport = passport.Text.Trim(), Seat = seat, Price = f.Price };
            db.Data.Tickets.Add(ticket); db.Save();
            dlg.DialogResult = DialogResult.OK; dlg.Close();
            RefreshAll();
            MessageBox.Show($"Билет успешно оформлен!\nРейс: {f.Number}\nМесто: {seat}\nНомер бронирования: {ticket.Id:N}",
                "Покупка выполнена", MessageBoxButtons.OK, MessageBoxIcon.Information);
            tabs.SelectedTab = myTab;
        };
        dlg.ShowDialog(this);
    }

    private void CancelTicket()
    {
        Ticket? t = SelectedTicket();
        if (t == null) { Warn("Выберите билет."); return; }
        if (t.Cancelled) { Warn("Этот билет уже отменён."); return; }
        var f = db.Data.Flights.FirstOrDefault(x => x.Id == t.FlightId);
        if (f == null || f.Departure <= DateTime.Now) { Warn("Нельзя отменить билет на прошедший рейс."); return; }
        if (MessageBox.Show("Отменить билет? Возврат денег в демоверсии не выполняется.", "Подтверждение",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        t.Cancelled = true; db.Save(); RefreshAll();
    }

    private void ShowTicket()
    {
        Ticket? t = SelectedTicket();
        if (t == null) { Warn("Выберите билет."); return; }
        Flight? f = db.Data.Flights.FirstOrDefault(x => x.Id == t.FlightId);
        if (f == null) { Warn("Рейс не найден."); return; }
        using var dlg = Dialog("Электронный авиабилет", 530, 450);
        string content = $"ЭЛЕКТРОННЫЙ АВИАБИЛЕТ\r\n\r\nБронирование: {t.Id:N}\r\nПассажир: {t.Passenger}\r\nДокумент: {Mask(t.Passport)}\r\n" +
            $"Рейс: {f.Number} / {f.Airline}\r\nМаршрут: {f.From} → {f.To}\r\nВылет: {f.Departure:dd.MM.yyyy HH:mm}\r\n" +
            $"Прилёт: {f.Arrival:dd.MM.yyyy HH:mm}\r\nМесто: {t.Seat}\r\nЦена: {t.Price:N0} ₽\r\n" +
            $"Статус: {(t.Cancelled ? "ОТМЕНЁН" : "ОФОРМЛЕН")}\r\n\r\nДЕМОНСТРАЦИОННЫЙ БИЛЕТ — НЕ ДЛЯ ПОСАДКИ";
        var area = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Top, Height = 315, Text = content, Font = new Font("Consolas", 10) };
        var save = Button("Сохранить TXT"); save.Location = new Point(20, 330);
        save.Click += (_, _) =>
        {
            using var sd = new SaveFileDialog { Filter = "Текстовый файл|*.txt", FileName = $"ticket_{t.Id:N}.txt" };
            if (sd.ShowDialog(dlg) == DialogResult.OK) { File.WriteAllText(sd.FileName, content); MessageBox.Show("Билет сохранён."); }
        };
        dlg.Controls.Add(area); dlg.Controls.Add(save);
        dlg.ShowDialog(this);
    }

    private static string Mask(string s) => s.Length <= 4 ? "****" : new string('*', s.Length - 4) + s[^4..];

    private void EditFlight(Flight? original)
    {
        if (current?.IsAdmin != true) return;
        using var dlg = Dialog(original == null ? "Добавление рейса" : "Редактирование рейса", 520, 630);
        var number = Field(dlg, "Номер рейса", 20, 12);
        var airline = Field(dlg, "Авиакомпания", 255, 12);
        var origin = Field(dlg, "Откуда", 20, 90);
        var destination = Field(dlg, "Куда", 255, 90);
        var departLabel = new Label { Text = "Вылет", Location = new Point(20, 175), AutoSize = true };
        var arrivalLabel = new Label { Text = "Прилёт", Location = new Point(255, 175), AutoSize = true };
        var depart = new DateTimePicker { Location = new Point(20, 202), Width = 210, Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy HH:mm", ShowUpDown = false };
        var arrival = new DateTimePicker { Location = new Point(255, 202), Width = 210, Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy HH:mm", ShowUpDown = false };
        var priceLabel = new Label { Text = "Цена (₽)", Location = new Point(20, 262), AutoSize = true };
        var price = new NumericUpDown { Location = new Point(20, 290), Width = 210, Maximum = 10000000,
            DecimalPlaces = 2, Minimum = 1, Value = 5000 };
        var seatsLabel = new Label { Text = "Количество мест", Location = new Point(255, 262), AutoSize = true };
        var seats = new NumericUpDown { Location = new Point(255, 290), Width = 210, Minimum = 1, Maximum = 1000, Value = 50 };
        var submit = Button("Сохранить рейс"); submit.Location = new Point(20, 373); submit.Width = 200;
        dlg.Controls.AddRange(new Control[] { departLabel, arrivalLabel, depart, arrival,
            priceLabel, price, seatsLabel, seats, submit });
        if (original != null)
        {
            number.Text = original.Number; airline.Text = original.Airline;
            origin.Text = original.From; destination.Text = original.To;
            depart.Value = original.Departure; arrival.Value = original.Arrival;
            price.Value = Math.Clamp(original.Price, price.Minimum, price.Maximum);
            seats.Value = Math.Clamp(original.Seats, (int)seats.Minimum, (int)seats.Maximum);
        }
        else { depart.Value = DateTime.Now.AddDays(1); arrival.Value = depart.Value.AddHours(2); }
        submit.Click += (_, _) =>
        {
            if (new[] { number.Text, airline.Text, origin.Text, destination.Text }.Any(string.IsNullOrWhiteSpace))
            { Warn("Заполните все текстовые поля."); return; }
            if (origin.Text.Trim().Equals(destination.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            { Warn("Города отправления и прибытия должны отличаться."); return; }
            if (depart.Value >= arrival.Value || depart.Value < DateTime.Now)
            { Warn("Проверьте дату вылета и прилёта (вылет должен быть в будущем)."); return; }
            int reserved = original == null ? 0 : db.Data.Tickets.Count(t => t.FlightId == original.Id && !t.Cancelled);
            if (seats.Value < reserved) { Warn("Нельзя указать меньше мест, чем уже продано билетов."); return; }
            Flight f = original ?? new Flight();
            f.Number = number.Text.Trim(); f.Airline = airline.Text.Trim();
            f.From = origin.Text.Trim(); f.To = destination.Text.Trim();
            f.Departure = depart.Value; f.Arrival = arrival.Value;
            f.Price = price.Value; f.Seats = (int)seats.Value;
            if (original == null) db.Data.Flights.Add(f);
            db.Save(); UpdateCities(); RefreshAll(); dlg.DialogResult = DialogResult.OK; dlg.Close();
        };
        dlg.ShowDialog(this);
    }

    private void DeleteFlight()
    {
        if (current?.IsAdmin != true) return;
        Flight? f = SelectedAdmin(); if (f == null) { Warn("Выберите рейс."); return; }
        if (db.Data.Tickets.Any(t => t.FlightId == f.Id))
        { Warn("Нельзя удалить рейс, по которому есть история билетов."); return; }
        if (MessageBox.Show($"Удалить рейс {f.Number}?", "Подтверждение", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        db.Data.Flights.Remove(f); db.Save(); UpdateCities(); RefreshAll();
    }

    private static Form Dialog(string title, int width, int height) => new()
    {
        Text = title, Size = new Size(width, height), StartPosition = FormStartPosition.CenterParent,
        FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
        Font = new Font("Segoe UI", 10), BackColor = Color.White
    };

    private static TextBox Field(Control parent, string title, int x, int y)
    {
        parent.Controls.Add(new Label { Text = title, AutoSize = true, Location = new Point(x, y) });
        var box = new TextBox { Location = new Point(x, y + 28), Width = 210 };
        parent.Controls.Add(box);
        return box;
    }

    private static void Warn(string text) => MessageBox.Show(text, "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
