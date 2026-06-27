namespace TelegramCarsBot.States;

public enum UserStep
{
    None,
    // Add steps
    AwaitingVinCode,
    AwaitingDescription,
    AwaitingImages,
    AwaitingSendDate,
    AwaitingArriveDate,
    AwaitingArriveTraiDate,
    // Update steps
    AwaitingUpdateField,
    AwaitingUpdateValue
}

public class UserSession
{
    public UserStep Step { get; set; } = UserStep.None;
    public int? EditingCarId { get; set; }

    // Temporary data while adding
    public string? VinCode { get; set; }
    public string? Description { get; set; }
    public List<string> Images { get; set; } = new();
    public DateTime? SendDate { get; set; }
    public DateTime? ArriveDate { get; set; }
}

public static class SessionStore
{
    private static readonly Dictionary<long, UserSession> _sessions = new();

    public static UserSession Get(long userId)
    {
        if (!_sessions.ContainsKey(userId))
            _sessions[userId] = new UserSession();
        return _sessions[userId];
    }

    public static void Reset(long userId)
    {
        _sessions[userId] = new UserSession();
    }
}
