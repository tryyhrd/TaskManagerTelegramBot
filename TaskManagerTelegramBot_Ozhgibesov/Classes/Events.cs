using System.ComponentModel.DataAnnotations.Schema;

namespace TaskManagerTelegramBot_Ozhgibesov.Classes
{
    public class Events
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Time { get; set; }
        public string Message { get; set; }
        public bool IsRecurring {  get; set; }

        public List<DayOfWeek>? RecurringDays { get; set; }
        
        public string? RecurringTimeStr { get; set; } = "00:00";

        [NotMapped]
        public TimeSpan RecurringTime
        {
            get => TimeSpan.Parse(RecurringTimeStr);
            set => RecurringTimeStr = value.ToString(@"hh\:mm");
        }

        public long UserId { get; set; }
        public Users User { get; set; } = null!;

        public Events() { }
        public Events(DateTime time, string message)
        {
            Time = time;
            Message = message;
        }
    }
}
