using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramCarsBot.Services;

namespace TelegramCarsBot.Handlers;

public class UpdateHandler(ITelegramBotClient bot, CarService carService, IConfiguration configuration)
{
    private readonly ITelegramBotClient _bot = bot;
    private readonly CarService _carService = carService;
    private readonly IConfiguration _configuration = configuration; // Внедряем конфиг

    [Obsolete]
    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } messageText } message) return;
        long chatId = message.Chat.Id;
        long userId = message.From?.Id ?? 0; // Получаем ID того, кто пишет боту

        // Читаем ID админа из appsettings / Render Environment
        long adminId = _configuration.GetValue<long>("AdminId"); 
        bool isAdmin = userId == adminId;

        // 1. Команда СТАРТ (Выдача клавиатуры по ролям)
        if (messageText == "/start")
        {
            ReplyKeyboardMarkup replyKeyboard;

            if (isAdmin)
            {
                // Меню для ТЕБЯ (Админ видит ВСЁ)
                replyKeyboard = new ReplyKeyboardMarkup(
                [
                    [
                        "🚗 Список машин",
                        "➕ Добавить машину"
                    ]
                ])
                {
                    ResizeKeyboard = true
                };
            }
            else
            {
                // Меню для КЛИЕНТОВ (Обычный юзер видит ТОЛЬКО список)
                replyKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "🚗 Список машин" }
                })
                {
                    ResizeKeyboard = true
                };
            }

            string welcomeText = isAdmin 
                ? "👋 Добро пожаловать в панель управления, Шеф!" 
                : "👋 Добро пожаловать! Выберите действие на клавиатуре:";

            await _bot.SendTextMessageAsync(chatId, welcomeText, replyMarkup: replyKeyboard, cancellationToken: ct);
            return;
        }

        // 2. Защита кнопки добавления
        if (messageText == "➕ Добавить машину" && !isAdmin)
        {
            await _bot.SendTextMessageAsync(chatId, "❌ У вас нет прав администратора для добавления машин.", cancellationToken: ct);
            return;
        }

        // 3. Вывод списка машин (доступно всем)
        if (messageText == "🚗 Список машин")
        {
            var cars = await _carService.GetAllAsync();

            if (cars.Count == 0)
            {
                await _bot.SendTextMessageAsync(chatId, "📭 Список автомобилей пока пуст.", cancellationToken: ct);
                return;
            }

            await _bot.SendTextMessageAsync(chatId, $" Найдено машин в базе: {cars.Count}. Вывожу список...", cancellationToken: ct);

            foreach (var car in cars)
            {
                string carInfo = $"🆔 **ID:** {car.Id}\n" +
                                 $"📌 **VIN:** `{car.VinCode}`\n" +
                                 $"📝 **Описание:** {car.Description}\n\n" +
                                 $"📅 **Отправка:** {car.SendDate:dd.MM.yyyy}\n" +
                                 $"🏁 **Прибытие:** {car.ArriveDate:dd.MM.yyyy}\n" +
                                 $"🚂 **Трейлер:** {car.ArriveTraiDate:dd.MM.yyyy}";

                if (!string.IsNullOrEmpty(car.Image1))
                {
                    await _bot.SendPhotoAsync(
                        chatId: chatId,
                        photo: InputFile.FromString(car.Image1),
                        caption: carInfo,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                        cancellationToken: ct
                    );
                }
                else
                {
                    await _bot.SendTextMessageAsync(chatId, carInfo, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, cancellationToken: ct);
                }
            }
            return;
        }

        // 4. Твоя логика стейт-машины (ввод VIN, фото, дат)
        // Чтобы левые люди не ломали пошаговый ввод, проверяем права перед обработкой стейтов:
        if (!isAdmin)
        {
            await _bot.SendTextMessageAsync(chatId, "❌ Неизвестная команда.", cancellationToken: ct);
            return;
        }

        // ТУТ НАЧИНАЕТСЯ ТВОЙ СТАРЫЙ КОД СТЕЙТ-МАШИНЫ ДОБАВЛЕНИЯ МАШИНЫ...
        // (Оставь его ниже без изменений, он будет защищен проверкой выше!)
    }
}
