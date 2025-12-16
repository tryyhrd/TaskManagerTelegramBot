using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using TaskManagerTelegramBot_Ozhgibesov.Classes;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TaskManagerTelegramBot_Ozhgibesov
{
    public class Worker : BackgroundService
    {
        readonly string Token = "8478761032:AAFvQ6jCTfbPbGIoLdXwi6KdJu47SRV-NL4";

        TelegramBotClient telegramBotClient;

        readonly ILogger<Worker> _logger;

        Timer Timer;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;

            using (var db = new Classes.Common.Connect())
            {
                db.Database.EnsureCreated();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            telegramBotClient = new TelegramBotClient(Token);
            telegramBotClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                null,
                new CancellationTokenSource().Token
                );

            TimerCallback TimerCallback = new TimerCallback(Tick);

            Timer = new Timer(TimerCallback, 0, 0, 60 * 1000);
        }

        List<string> Messages = new List<string>()
        {
            "Здравствуйте! " +
            "\nРады приветствовать вас в Telegram-боте Напоминаторе",

            "Укажите дату и время напоминания в следующем формате:" +
            "\n<i><b>12:51 26.04.2025</b>" +
            "\nНапомни о том что я хотел сходить в магазин.</i>",

            "",
            "Задачи пользователя не найдены.",
            "Событие удалено.",
            "Все события удалены.",
            "Событие добавлено."
        };

        public static bool CheckFormatDateTime(string value, out DateTime time)
        {
            return DateTime.TryParse(value, out time);
        }

        public static ReplyKeyboardMarkup GetButtons()
        {
            List<KeyboardButton> keyboardButtons = new List<KeyboardButton>();
            keyboardButtons.Add(new KeyboardButton("Удалить все задачи"));

            return new ReplyKeyboardMarkup { Keyboard = new List<List<KeyboardButton>> { keyboardButtons } };
        }

        public static InlineKeyboardMarkup DeleteEvent(string eventId)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton("Удалить", callbackDataOrUrl: eventId)
            });
        }

        public async void SendMessage(long chatId, int typeMessage)
        {
            if (typeMessage != 2)
            {
                await telegramBotClient.SendMessage(
                    chatId,
                    Messages[typeMessage],
                    ParseMode.Html,
                    replyMarkup: GetButtons());
            }
            else if (typeMessage == 2)
                await telegramBotClient.SendMessage(
                    chatId,
                    $"Указанное вами время и дата не могут быть установлены," +
                    $"потому-что сейчас уже: {DateTime.Now.ToString("HH.mm dd.MM.yyyy")}");
        }

        public async void Command(long chatId, string command)
        {
            if (command.ToLower() == "/start") SendMessage(chatId, 0);
            else if (command.ToLower() == "/create_task") SendMessage(chatId, 1);
            else if (command.ToLower() == "/list_tasks")
            {
                using (var db = new Classes.Common.Connect())
                {
                    Users User = await db.Users
                            .Include(u => u.Events)
                            .FirstOrDefaultAsync(x => x.IdUser == chatId);

                    if (User == null) SendMessage(chatId, 3);
                    else if (User.Events.Count == 0) SendMessage(chatId, 3);
                    else
                    {
                        foreach (Events Event in User.Events)
                        {
                            await telegramBotClient.SendMessage(
                                chatId,
                                $"Уведомить пользователя: {Event.Time.ToString("HH:mm dd:MM:yyyy")}" +
                                $"\nСообщение: {Event.Message}",
                                replyMarkup: DeleteEvent(Event.Id)
                                );
                        }
                    }
                }
            }
        }

        private async Task GetMessages(Message message)
        {
            Console.WriteLine("Получено сообщение: " + message.Text + " от пользователя: " + message.Chat.Username);

            long IdUser = message.Chat.Id;
            string MessageUser = message.Text;

            Users User = null;

            if (MessageUser.Contains("/")) Command(message.Chat.Id, MessageUser);

            else if (message.Text.Equals("Удалить все задачи"))
            {
                using (var db = new Classes.Common.Connect())
                {
                    User = await db.Users.Include(u => u.Events).FirstOrDefaultAsync(x => x.IdUser == IdUser);

                    if (User == null) SendMessage(message.Chat.Id, 3);
                    else if (User.Events?.Count == 0 || User.Events == null) SendMessage(User.IdUser, 3);
                    else
                    {
                        var userInDb = db.Users.Include(u => u.Events).First(u => u.IdUser == IdUser);
                        db.Events.RemoveRange(userInDb.Events);
                        db.SaveChanges();

                        var userInMemory = db.Users.First(u => u.IdUser == IdUser);
                        userInMemory.Events.Clear();

                        SendMessage(User.IdUser, 5);
                    }
                }    
            }
            else
            {
                using (var db = new Classes.Common.Connect())
                {
                    var user = db.Users
                        .Include(u => u.Events)
                        .FirstOrDefault(u => u.IdUser == IdUser);

                    if (user == null)
                    {
                        user = new Users(IdUser);
                        db.Users.Add(user);
                        await db.SaveChangesAsync();
                    }

                    string text = message.Text.Trim();

                    if (text.StartsWith("каждую", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("каждый", StringComparison.OrdinalIgnoreCase))
                    {
                        var lines = message.Text.Split('\n', 2);
                        if (lines.Length < 2) return;

                        var scheduleLine = lines[0];
                        var taskMessage = lines[1];

                        var days = new List<DayOfWeek>();
                        if (scheduleLine.Contains("понедельник")) days.Add(DayOfWeek.Monday);
                        if (scheduleLine.Contains("вторник")) days.Add(DayOfWeek.Tuesday);
                        if (scheduleLine.Contains("среду") || scheduleLine.Contains("среда")) days.Add(DayOfWeek.Wednesday);
                        if (scheduleLine.Contains("четверг")) days.Add(DayOfWeek.Thursday);
                        if (scheduleLine.Contains("пятницу")) days.Add(DayOfWeek.Friday);
                        if (scheduleLine.Contains("субботу")) days.Add(DayOfWeek.Saturday);
                        if (scheduleLine.Contains("воскресенье") || scheduleLine.Contains("воскресенья")) days.Add(DayOfWeek.Sunday);

                        var timeMatch = Regex.Match(scheduleLine, @"(\d{1,2}):(\d{2})");
                        if (!timeMatch.Success || days.Count == 0)
                        {
                            await telegramBotClient.SendMessage(IdUser, "Не удалось распознать расписание.");
                            return;
                        }

                        var hour = int.Parse(timeMatch.Groups[1].Value);
                        var minute = int.Parse(timeMatch.Groups[2].Value);
                        var recurringTime = new TimeSpan(hour, minute, 0);

                        var now = DateTime.Now;
                        var nextTrigger = CalculateNextRecurrence(now, days, recurringTime);

                        var recurringEvent = new Events
                        {
                            Message = taskMessage,
                            Time = nextTrigger,
                            IsRecurring = true,
                            RecurringDays = days,
                            RecurringTimeStr = $"{hour:D2}:{minute:D2}",
                            UserId = IdUser
                        };

                        user.Events.Add(recurringEvent);
                        await db.SaveChangesAsync();

                        await telegramBotClient.SendMessage(IdUser, $"Повторяющаяся задача добавлена!\nСледующее напоминание: {nextTrigger:dd.MM.yyyy HH:mm}");
                        return;
                    }
                    
                    string[] info = message.Text.Split('\n');

                    if (info.Length < 2)
                    {
                        SendMessage(message.Chat.Id, 2);
                        return;
                    }

                    DateTime time;

                    if (CheckFormatDateTime(info[0], out time) == false)
                    {
                        SendMessage(message.Chat.Id, 2);
                        return;
                    }

                    if (time < DateTime.Now) SendMessage(message.Chat.Id, 3);

                    var Event = new Events
                    {
                        Message = message.Text.Replace("HH:mm dd.MM.yyyy" + "\n", ""),
                        Time = time,
                        IsRecurring = false,
                        RecurringDays = null,
                        RecurringTimeStr = null,
                        UserId = IdUser
                    };

                    var newEvent = new Events(time, message.Text.Replace("HH:mm dd.MM.yyyy" + "\n", ""));
                    user.Events.Add(newEvent);

                    await db.SaveChangesAsync();

                    await telegramBotClient.SendMessage(
                            chatId: IdUser,
                            text: $"Задача добавлена!"
                                );
                }
            }
        }

        private DateTime CalculateNextRecurrence(DateTime from, List<DayOfWeek> days, TimeSpan time)
        {
            var current = from.Date.Add(time);
            if (current <= from)
                current = current.AddDays(1);

            while (!days.Contains(current.DayOfWeek))
            {
                current = current.AddDays(1);
            }
            return current;
        }

        private async Task HandleUpdateAsync(
            ITelegramBotClient client,
            Update update,
            CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message)
                await GetMessages(update.Message);

            else if (update.Type == UpdateType.CallbackQuery)
            {
                var query = update.CallbackQuery;
                var userId = query.Message.Chat.Id;
                var eventId = query.Data;

                using (var db = new Classes.Common.Connect())
                {
                    var User = db.Users.Include(u => u.Events).First(x => x.IdUser == userId);

                    if (User != null)
                    {
                        var eventToRemove = User.Events.FirstOrDefault(e => e.Id == eventId);

                        if (eventToRemove != null)
                        {
                            db.Events.Remove(eventToRemove);
                            await db.SaveChangesAsync();

                            await telegramBotClient.AnswerCallbackQuery(
                                    callbackQueryId: query.Id,
                                    text: "Событие удалено."
                                    );

                            await telegramBotClient.DeleteMessage(query.Message.Chat.Id, query.Message.Id, cancellationToken);
                        }
                    }
                }
            }
        }

        private async Task HandleErrorAsync(
            ITelegramBotClient client,
            Exception exception,
            HandleErrorSource source,
            CancellationToken cancellationToken)
        {
            Console.WriteLine("Ошибка: " + exception.Message);
        }

        public async void Tick(object obj)
        {
            var now = DateTime.Now;
            var start = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            var end = start.AddMinutes(1);

            using (var db = new Classes.Common.Connect())
            {
                var events = db.Events
                    .Include(e => e.User) 
                    .Where(e => e.Time >= start && e.Time < end)
                    .ToList();

                foreach (var ev in events)
                {
                    try
                    {
                        await telegramBotClient.SendMessage(
                            ev.User.IdUser,
                            $"Напоминание: {ev.Message}"
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка отправки: {ex.Message}");
                    }

                    if (ev.IsRecurring)
                    {
                        var timeParts = ev.RecurringTimeStr.Split(':');
                        var recurringTime = new TimeSpan(
                            int.Parse(timeParts[0]),
                            int.Parse(timeParts[1]),
                            0
                        );
                        ev.Time = CalculateNextRecurrence(DateTime.Now, ev.RecurringDays, recurringTime);
                    }
                    else
                    {
                        db.Events.Remove(ev);
                    }
                }

                if (events.Any())
                    await db.SaveChangesAsync();
            }
        }
    }
}
