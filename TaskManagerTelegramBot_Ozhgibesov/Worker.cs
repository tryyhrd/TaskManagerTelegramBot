using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Security;
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

        ILogger<Worker> _logger;

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
            "Все события удалены."
        };

        public bool CheckFormatDateTime(string value, out DateTime time)
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
                    Users User = db.Users
                            .Include(u => u.Events)
                            .FirstOrDefault(x => x.IdUser == chatId);

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

        private void GetMessages(Message message)
        {
            Console.WriteLine("Получено сообщение: " + message.Text + " от пользователя: " + message.Chat.Username);

            long IdUser = message.Chat.Id;
            string MessageUser = message.Text;

            Users User = null;

            if (message.Text.Contains("/")) Command(message.Chat.Id, message.Text);

            else if (message.Text.Equals("Удалить все задачи"))
            {
                using (var db = new Classes.Common.Connect())
                {
                    User = db.Users.FirstOrDefault(x => x.IdUser == IdUser);

                    if (User == null) SendMessage(message.Chat.Id, 4);
                    else if (User.Events.Count == 0) SendMessage(User.IdUser, 3);
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

                    var newEvent = new Events(time, message.Text.Replace("HH:mm dd.MM.yyyy" + "\n", ""));
                    user.Events.Add(newEvent);

                    db.SaveChanges(); 
                }
            }
        }

        private async Task HandleUpdateAsync(
            ITelegramBotClient client,
            Update update,
            CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message)
                GetMessages(update.Message);

            else if (update.Type == UpdateType.CallbackQuery)
            {
                var query = update.CallbackQuery;
                var userId = query.Message.Chat.Id;
                var eventId = query.Data;

                using (var db = new Classes.Common.Connect())
                {
                    var User = db.Users.First(x => x.IdUser == userId);

                    if (User != null)
                    {
                        var eventToRemove = User.Events.Find(e => e.Id == eventId);
                        if (eventToRemove != null)
                        {
                            User.Events.Remove(eventToRemove);
                            await telegramBotClient.SendMessage(userId, "Событие удалено.");
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

                    db.Events.Remove(ev);
                }

                if (events.Any())
                    db.SaveChanges();
            }
        }
    }
}
