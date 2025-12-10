using TaskManagerTelegramBot_Ozhgibesov.Classes;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TaskManagerTelegramBot_Ozhgibesov
{
    public class Worker : BackgroundService
    {
        readonly string Token = "полученный телеграмм токен";

        TelegramBotClient telegramBotClient;

        List<Users> Users = new List<Users>();

        ILogger<Worker> _logger;

        Timer Timer;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
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

        public static InlineKeyboardMarkup DeleteEvent(string Message)
        {
            List<InlineKeyboardButton> inlineKeyboards = new List<InlineKeyboardButton>();

            inlineKeyboards.Add(new InlineKeyboardButton("Удалить", Message));

            return new InlineKeyboardMarkup(inlineKeyboards);
        }

        public async void SendMessage(long chatId, int typeMessage)
        {
            if (typeMessage != 3)
            {
                await telegramBotClient.SendMessage(
                    chatId,
                    Messages[typeMessage],
                    ParseMode.Html,
                    replyMarkup: GetButtons());
            }
            else if (typeMessage == 3)
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
                Users User = Users.Find(x => x.IdUser == chatId);

                if (User == null) SendMessage(chatId, 4);
                else if (User.Events.Count == 0) SendMessage(chatId, 4);
                else
                {
                    foreach (Events Event in User.Events)
                    {
                        await telegramBotClient.SendMessage(
                            chatId,
                            $"Уведомить пользователя: {Event.Time.ToString("HH:mm dd:MM:yyyy")}" +
                            $"\nСообщение: {Event.Message}",
                            replyMarkup: DeleteEvent(Event.Message)
                            ); 
                    }
                }
            }
        }
    }
}
