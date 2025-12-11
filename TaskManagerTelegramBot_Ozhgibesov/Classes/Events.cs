namespace TaskManagerTelegramBot_Ozhgibesov.Classes
{
    public class Events
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Time { get; set; }
        public string Message { get; set; }
        public Events(DateTime time, string message)
        {
            Time = time;
            Message = message;
        }
    }
}
