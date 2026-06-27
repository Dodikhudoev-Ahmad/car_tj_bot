namespace TelegramCarsBot.Models;

public class Car
{
    public int Id { get; set; }
    public string VinCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Image1 { get; set; }
    public string? Image2 { get; set; }
    public string? Image3 { get; set; }
    public string? Image4 { get; set; }
    public string? Image5 { get; set; }
    public DateTime SendDate { get; set; }
    public DateTime ArriveDate { get; set; }
    public DateTime ArriveTraiDate { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
}
