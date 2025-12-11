namespace TaskManagerTelegramBot_Ozhgibesov.Classes
{
    public class Events
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Time { get; set; }
        public string Message { get; set; }
        public int UserId { get; set; }
        public Users User { get; set; } = null!;
        public Events() { }
        public Events(DateTime time, string message)
        {
            Time = time;
            Message = message;
        }
    }
}
