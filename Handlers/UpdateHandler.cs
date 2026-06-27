using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramCarsBot.Models;
using TelegramCarsBot.Services;
using TelegramCarsBot.States;

namespace TelegramCarsBot.Handlers;

public class UpdateHandler(ITelegramBotClient bot, CarService carService)
{
    private static readonly string[] DateFormats = ["dd.MM.yyyy", "d.MM.yyyy", "dd.M.yyyy", "d.M.yyyy"];

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        if (update.Type == UpdateType.Message && update.Message is not null)
            await HandleMessageAsync(update.Message, ct);
    }

    private async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        var userId = msg.From!.Id;
        var session = SessionStore.Get(userId);

        if (msg.Text?.StartsWith("/") == true)
        {
            await HandleCommandAsync(msg, session, ct);
            return;
        }

        await HandleStepAsync(msg, session, ct);
    }

    private async Task HandleCommandAsync(Message msg, UserSession session, CancellationToken ct)
    {
        var text = msg.Text!.Trim();
        var chatId = msg.Chat.Id;
        var userId = msg.From!.Id;

        if (text == "/start")
        {
            SessionStore.Reset(userId);
            await bot.SendMessage(chatId,
                "🚗 *Cars Bot*\n\n" +
                "Доступные команды:\n" +
                "/add — добавить машину\n" +
                "/list — список всех машин\n" +
                "/get <id> — детали машины\n" +
                "/delete <id> — удалить машину\n" +
                "/update <id> — обновить машину\n" +
                "/cancel — отменить действие",
                parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }

        if (text == "/cancel")
        {
            SessionStore.Reset(userId);
            await bot.SendMessage(chatId, "❌ Действие отменено.", cancellationToken: ct);
            return;
        }

        if (text == "/add")
        {
            SessionStore.Reset(userId);
            session = SessionStore.Get(userId);
            session.Step = UserStep.AwaitingVinCode;
            await bot.SendMessage(chatId, "🔑 Введите VIN-код машины (17 символов):", cancellationToken: ct);
            return;
        }

        if (text == "/list")
        {
            await HandleListAsync(chatId, ct);
            return;
        }

        if (text.StartsWith("/get "))
        {
            if (int.TryParse(text[5..], out var id))
                await HandleGetAsync(chatId, id, ct);
            else
                await bot.SendMessage(chatId, "⚠️ Укажите корректный ID. Пример: /get 1", cancellationToken: ct);
            return;
        }

        if (text.StartsWith("/delete "))
        {
            if (int.TryParse(text[8..], out var id))
                await HandleDeleteAsync(chatId, id, ct);
            else
                await bot.SendMessage(chatId, "⚠️ Укажите корректный ID. Пример: /delete 1", cancellationToken: ct);
            return;
        }

        if (text.StartsWith("/update "))
        {
            if (int.TryParse(text[8..], out var id))
            {
                var car = await carService.GetByIdAsync(id);
                if (car is null)
                {
                    await bot.SendMessage(chatId, $"❌ Машина #{id} не найдена.", cancellationToken: ct);
                    return;
                }
                session.Step = UserStep.AwaitingUpdateField;
                session.EditingCarId = id;
                await bot.SendMessage(chatId,
                    $"✏️ Что хотите обновить в машине #{id}?\n\n" +
                    "Напишите одно из:\n" +
                    "`vin` — VIN-код\n" +
                    "`description` — описание\n" +
                    "`senddate` — дата отправки\n" +
                    "`arrivedate` — дата прибытия\n" +
                    "`arrivetraidate` — дата прибытия трейлера",
                    parseMode: ParseMode.Markdown, cancellationToken: ct);
            }
            else
            {
                await bot.SendMessage(chatId, "⚠️ Укажите корректный ID. Пример: /update 1", cancellationToken: ct);
            }
            return;
        }

        await bot.SendMessage(chatId, "❓ Неизвестная команда. Напишите /start для списка команд.", cancellationToken: ct);
    }

    private async Task HandleStepAsync(Message msg, UserSession session, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var userId = msg.From!.Id;

        switch (session.Step)
        {
            case UserStep.AwaitingVinCode:
                var vin = msg.Text?.Trim().ToUpper();
                if (string.IsNullOrEmpty(vin) || vin.Length != 17)
                {
                    await bot.SendMessage(chatId, "⚠️ VIN-код должен быть ровно 17 символов. Попробуйте ещё раз:", cancellationToken: ct);
                    return;
                }
                session.VinCode = vin;
                session.Step = UserStep.AwaitingDescription;
                await bot.SendMessage(chatId, "📝 Введите описание машины:", cancellationToken: ct);
                break;

            case UserStep.AwaitingDescription:
                session.Description = msg.Text?.Trim() ?? "";
                session.Step = UserStep.AwaitingImages;
                await bot.SendMessage(chatId,
                    "📸 Отправьте фото машины (до 5 штук).\n\nКогда закончите — напишите *готово*.",
                    parseMode: ParseMode.Markdown, cancellationToken: ct);
                break;

            case UserStep.AwaitingImages:
                if (msg.Text?.ToLower() == "готово")
                {
                    session.Step = UserStep.AwaitingSendDate;
                    await bot.SendMessage(chatId, "📅 Введите дату отправки (формат: дд.мм.гггг):", cancellationToken: ct);
                    return;
                }
                if (msg.Photo is not null && session.Images.Count < 5)
                {
                    var fileId = msg.Photo.Last().FileId;
                    session.Images.Add(fileId);
                    await bot.SendMessage(chatId,
                        $"✅ Фото {session.Images.Count}/5 сохранено. Отправьте ещё или напишите *готово*.",
                        parseMode: ParseMode.Markdown, cancellationToken: ct);
                }
                else if (session.Images.Count >= 5)
                {
                    session.Step = UserStep.AwaitingSendDate;
                    await bot.SendMessage(chatId, "📅 Максимум фото достигнут. Введите дату отправки (формат: дд.мм.гггг):", cancellationToken: ct);
                }
                break;

            case UserStep.AwaitingSendDate:
                if (!TryParseDate(msg.Text, out var sendDate))
                {
                    await bot.SendMessage(chatId, "⚠️ Неверный формат. Введите дату в формате дд.мм.гггг:", cancellationToken: ct);
                    return;
                }
                session.SendDate = sendDate;
                session.Step = UserStep.AwaitingArriveDate;
                await bot.SendMessage(chatId, "📅 Введите дату прибытия (формат: дд.мм.гггг):", cancellationToken: ct);
                break;

            case UserStep.AwaitingArriveDate:
                if (!TryParseDate(msg.Text, out var arriveDate))
                {
                    await bot.SendMessage(chatId, "⚠️ Неверный формат. Введите дату в формате дд.мм.гггг:", cancellationToken: ct);
                    return;
                }
                session.ArriveDate = arriveDate;
                session.Step = UserStep.AwaitingArriveTraiDate;
                await bot.SendMessage(chatId, "📅 Введите дату прибытия трейлера (формат: дд.мм.гггг):", cancellationToken: ct);
                break;

            case UserStep.AwaitingArriveTraiDate:
                if (!TryParseDate(msg.Text, out var arriveTraiDate))
                {
                    await bot.SendMessage(chatId, "⚠️ Неверный формат. Введите дату в формате дд.мм.гггг:", cancellationToken: ct);
                    return;
                }
                var car = new Car
                {
                    VinCode        = session.VinCode!,
                    Description    = session.Description ?? "",
                    Image1         = session.Images.ElementAtOrDefault(0),
                    Image2         = session.Images.ElementAtOrDefault(1),
                    Image3         = session.Images.ElementAtOrDefault(2),
                    Image4         = session.Images.ElementAtOrDefault(3),
                    Image5         = session.Images.ElementAtOrDefault(4),
                    SendDate       = session.SendDate!.Value,
                    ArriveDate     = session.ArriveDate!.Value,
                    ArriveTraiDate = arriveTraiDate
                };
                var saved = await carService.AddAsync(car);
                SessionStore.Reset(userId);
                await bot.SendMessage(chatId,
                    $"✅ Машина добавлена!\n\n" +
                    $"🆔 ID: `{saved.Id}`\n" +
                    $"🔑 VIN: `{saved.VinCode}`",
                    parseMode: ParseMode.Markdown, cancellationToken: ct);
                break;

            case UserStep.AwaitingUpdateField:
                var field = msg.Text?.ToLower().Trim();
                var validFields = new[] { "vin", "description", "senddate", "arrivedate", "arrivetraidate" };
                if (!validFields.Contains(field))
                {
                    await bot.SendMessage(chatId, "⚠️ Неверное поле. Напишите одно из: vin, description, senddate, arrivedate, arrivetraidate", cancellationToken: ct);
                    return;
                }
                session.Description = field;
                session.Step = UserStep.AwaitingUpdateValue;
                await bot.SendMessage(chatId, $"✏️ Введите новое значение для *{field}*:", parseMode: ParseMode.Markdown, cancellationToken: ct);
                break;

            case UserStep.AwaitingUpdateValue:
                var fieldName = session.Description;
                var newValue = msg.Text?.Trim();
                var carId = session.EditingCarId!.Value;

                var updated = await carService.UpdateAsync(carId, c =>
                {
                    switch (fieldName)
                    {
                        case "vin":
                            c.VinCode = newValue?.ToUpper() ?? c.VinCode;
                            break;
                        case "description":
                            c.Description = newValue ?? c.Description;
                            break;
                        case "senddate":
                            if (TryParseDate(newValue, out var sd)) c.SendDate = sd;
                            break;
                        case "arrivedate":
                            if (TryParseDate(newValue, out var ad)) c.ArriveDate = ad;
                            break;
                        case "arrivetraidate":
                            if (TryParseDate(newValue, out var atd)) c.ArriveTraiDate = atd;
                            break;
                    }
                });

                SessionStore.Reset(userId);
                if (updated is not null)
                    await bot.SendMessage(chatId, $"✅ Машина #{carId} обновлена!", cancellationToken: ct);
                else
                    await bot.SendMessage(chatId, $"❌ Машина #{carId} не найдена.", cancellationToken: ct);
                break;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static bool TryParseDate(string? input, out DateTime result)
    {
        var success = DateTime.TryParseExact(
            input?.Trim(),
            DateFormats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out result);
        if (success)
            result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
        return success;
    }

    // ─── CRUD Handlers ────────────────────────────────────────────────────────

    private async Task HandleListAsync(long chatId, CancellationToken ct)
    {
        var cars = await carService.GetAllAsync();
        if (cars.Count == 0)
        {
            await bot.SendMessage(chatId, "📭 Список машин пуст.", cancellationToken: ct);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("🚗 *Список машин:*\n");
        foreach (var c in cars)
        {
            sb.AppendLine($"🆔 `{c.Id}` | 🔑 `{c.VinCode}`");
            sb.AppendLine($"📝 {c.Description[..Math.Min(50, c.Description.Length)]}...");
            sb.AppendLine($"📅 Отправка: {c.SendDate:dd.MM.yyyy}");
            sb.AppendLine();
        }
        sb.AppendLine("Для деталей: /get <id>");

        await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Markdown, cancellationToken: ct);
    }

    private async Task HandleGetAsync(long chatId, int id, CancellationToken ct)
    {
        var car = await carService.GetByIdAsync(id);
        if (car is null)
        {
            await bot.SendMessage(chatId, $"❌ Машина #{id} не найдена.", cancellationToken: ct);
            return;
        }

        var text =
            $"🚗 *Машина #{car.Id}*\n\n" +
            $"🔑 VIN: `{car.VinCode}`\n" +
            $"📝 Описание: {car.Description}\n" +
            $"📅 Дата отправки: {car.SendDate:dd.MM.yyyy}\n" +
            $"📅 Дата прибытия: {car.ArriveDate:dd.MM.yyyy}\n" +
            $"📅 Прибытие трейлера: {car.ArriveTraiDate:dd.MM.yyyy}\n" +
            $"🕐 Создано: {car.CreatedDate:dd.MM.yyyy HH:mm}\n" +
            $"🕐 Обновлено: {car.UpdatedDate:dd.MM.yyyy HH:mm}";

        await bot.SendMessage(chatId, text, parseMode: ParseMode.Markdown, cancellationToken: ct);

        var images = new[] { car.Image1, car.Image2, car.Image3, car.Image4, car.Image5 }
            .Where(i => !string.IsNullOrEmpty(i))
            .Select(i => i!)
            .ToList();

        if (images.Count == 1)
        {
            await bot.SendPhoto(chatId, InputFile.FromFileId(images[0]), cancellationToken: ct);
        }
        else if (images.Count > 1)
        {
            var mediaGroup = images
                .Select(fileId => new InputMediaPhoto(InputFile.FromFileId(fileId)))
                .Cast<IAlbumInputMedia>()
                .ToArray();
            await bot.SendMediaGroup(chatId, mediaGroup, cancellationToken: ct);
        }
    }

    private async Task HandleDeleteAsync(long chatId, int id, CancellationToken ct)
    {
        var deleted = await carService.DeleteAsync(id);
        if (deleted)
            await bot.SendMessage(chatId, $"✅ Машина #{id} удалена.", cancellationToken: ct);
        else
            await bot.SendMessage(chatId, $"❌ Машина #{id} не найдена.", cancellationToken: ct);
    }
}