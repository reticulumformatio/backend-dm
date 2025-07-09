using System.Text.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);


var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
            {
                policy.WithOrigins("https://dmunlimited.online",
                                   "http://localhost:3000") // Если нужно для разработки
                  .SetIsOriginAllowedToAllowWildcardSubdomains()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            });
});


builder.Services.AddHttpClient();

var app = builder.Build();

app.UseRouting();
app.UseCors(MyAllowSpecificOrigins);

app.MapPost("/api/send-to-telegram", async (
    TelegramMessageRequest request,
    IHttpClientFactory httpClientFactory, // Автоматически внедряется .NET
    IConfiguration configuration, // Автоматически внедряется для доступа к appsettings.json
    ILogger<Program> logger // Для логирования ошибок
) =>
{
    // Получаем токен и Chat ID из конфигурации
    var botToken = configuration["TelegramSettings:BotToken"];
    var chatId = configuration["TelegramSettings:ChatId"];

    var httpClient = httpClientFactory.CreateClient();
    var telegramApiUrl = $"https://api.telegram.org/bot{botToken}/sendMessage";

    // Формируем текст сообщения для Telegram
    var telegramText = $"Новое сообщение от DM Unlimited:\n" +
                       $"Имя: {request.Name}\n" +
                       $"Контакт: {request.Contact} ({request.ContactMethod})\n" +
                       $"Сообщение:\n{request.Message}";

    // Подготовка данных для отправки в Telegram API
    var telegramPayload = new
    {
        chat_id = chatId,
        text = telegramText,
        parse_mode = "HTML" // Можно использовать MarkdownV2 или HTML для форматирования
    };

    try
    {
        var response = await httpClient.PostAsJsonAsync(telegramApiUrl, telegramPayload);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Message successfully sent to Telegram.");
            return Results.Ok(new { success = true, message = "Сообщение успешно отправлено." });
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Failed to send message to Telegram. Status: {StatusCode}, Content: {ErrorContent}", response.StatusCode, errorContent);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while sending message to Telegram.");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
});

// --- ЭНДПОИНТ ДЛЯ TELEGRAM WEBHOOK ---
app.MapPost("/api/telegram-updates", async (Update update, ILogger<Program> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration) =>
{
    // ASP.NET Core автоматически десериализует входящий JSON в объект 'Update'.
    // Если десериализация не удалась (например, невалидный JSON), 'update' будет null.
    if (update == null)
    {
        logger.LogWarning("Получено пустое или невалидное обновление вебхука Telegram.");
        return Results.BadRequest("Invalid Telegram update.");
    }

    // Логируем полученное обновление.
    // Для более детального логирования можно сериализовать объект обратно в JSON.
    logger.LogInformation("Получено обновление вебхука Telegram. ID Обновления: {UpdateId}, Тип: {UpdateType}", update.UpdateId, update.Type);

    long chatId = update!.Message!.Chat!.Id;
    string messageText = update!.Message!.Text!;

    // Пример обработки текстового сообщения
    if (update.Message != null && !string.IsNullOrWhiteSpace(messageText))
    {
        logger.LogInformation("Сообщение из чата {ChatId}: {MessageText}", update.Message.Chat?.Id, update.Message.Text);
        logger.LogInformation("Сообщение отправлено пользователем: {UserName} (ID: {UserId})", update.Message.From?.Username ?? update.Message.From?.FirstName, update.Message.From?.Id);

        await SendMessage(chatId, messageText,httpClientFactory,configuration,logger);
    }
    else if (update.CallbackQuery != null)
    {
        logger.LogInformation("Получен Callback Query от пользователя ID: {UserId} с данными: {CallbackData}", update.CallbackQuery.From?.Id, update.CallbackQuery.Data);
        // Логика для обработки Callback Query
    }
    else
    {
        // Логируем другие типы обновлений, которые не обрабатываются явно
        logger.LogInformation("Получен необработанный тип обновления Telegram: {UpdateType}", update.Type);
    }

    // Telegram ожидает HTTP 200 OK в ответ на вебхук.
    // Это сигнализирует Telegram, что обновление успешно получено и обработано.
    // Всегда возвращаем 200 OK, чтобы избежать повторных отправок от Telegram.
    return Results.Ok();
});



app.Run();

static async Task SendMessage(long chatId, string MessageText,IHttpClientFactory httpClientFactory,IConfiguration configuration, ILogger<Program> logger){

    // Получаем токен и Chat ID из конфигурации
    var botToken = configuration["TelegramSettings:BotToken"];

    var httpClient = httpClientFactory.CreateClient();
    var telegramApiUrl = $"https://api.telegram.org/bot{botToken}/sendMessage";

 

    // Подготовка данных для отправки в Telegram API
    var telegramPayload = new
    {
        chat_id = chatId,
        text = MessageText,
        parse_mode = "HTML" // Можно использовать MarkdownV2 или HTML для форматирования
    };

    try
    {
        var response = await httpClient.PostAsJsonAsync(telegramApiUrl, telegramPayload);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Message successfully sent to Telegram.");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Failed to send message to Telegram. Status: {StatusCode}, Content: {ErrorContent}", response.StatusCode, errorContent);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while sending message to Telegram.");
    }
}

public record TelegramMessageRequest(
    string Name,
    string Contact,
    string Message,
    string ContactMethod
);


public class Update
{
    [JsonPropertyName("update_id")]
    public int UpdateId { get; set; }

    [JsonPropertyName("message")]
    public Message? Message { get; set; }

    [JsonPropertyName("edited_message")]
    public Message? EditedMessage { get; set; }

    [JsonPropertyName("channel_post")]
    public Message? ChannelPost { get; set; }

    [JsonPropertyName("edited_channel_post")]
    public Message? EditedChannelPost { get; set; }

    [JsonPropertyName("inline_query")]
    public InlineQuery? InlineQuery { get; set; }

    [JsonPropertyName("chosen_inline_result")]
    public ChosenInlineResult? ChosenInlineResult { get; set; }

    [JsonPropertyName("callback_query")]
    public CallbackQuery? CallbackQuery { get; set; }

    [JsonPropertyName("shipping_query")]
    public ShippingQuery? ShippingQuery { get; set; }

    [JsonPropertyName("pre_checkout_query")]
    public PreCheckoutQuery? PreCheckoutQuery { get; set; }

    [JsonPropertyName("poll")]
    public Poll? Poll { get; set; }

    [JsonPropertyName("poll_answer")]
    public PollAnswer? PollAnswer { get; set; }

    [JsonPropertyName("my_chat_member")]
    public ChatMemberUpdated? MyChatMember { get; set; }

    [JsonPropertyName("chat_member")]
    public ChatMemberUpdated? ChatMember { get; set; }

    [JsonPropertyName("chat_join_request")]
    public ChatJoinRequest? ChatJoinRequest { get; set; }

    // Вспомогательное свойство для определения типа обновления (для удобства логирования)
    public string Type
    {
        get
        {
            if (Message != null) return "Message";
            if (EditedMessage != null) return "EditedMessage";
            if (ChannelPost != null) return "ChannelPost";
            if (EditedChannelPost != null) return "EditedChannelPost";
            if (InlineQuery != null) return "InlineQuery";
            if (ChosenInlineResult != null) return "ChosenInlineResult";
            if (CallbackQuery != null) return "CallbackQuery";
            if (ShippingQuery != null) return "ShippingQuery";
            if (PreCheckoutQuery != null) return "PreCheckoutQuery";
            if (Poll != null) return "Poll";
            if (PollAnswer != null) return "PollAnswer";
            if (MyChatMember != null) return "MyChatMember";
            if (ChatMember != null) return "ChatMember";
            if (ChatJoinRequest != null) return "ChatJoinRequest";
            return "Unknown";
        }
    }
}

// Представляет сообщение в Telegram
public class Message
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("from")]
    public User? From { get; set; } // Отправитель сообщения

    [JsonPropertyName("sender_chat")]
    public Chat? SenderChat { get; set; } // Чат, от имени которого отправлено сообщение (для каналов)

    [JsonPropertyName("date")]
    public int Date { get; set; } // Дата отправки сообщения в Unix-времени

    [JsonPropertyName("chat")]
    public Chat? Chat { get; set; } // Чат, в котором было отправлено сообщение

    [JsonPropertyName("text")]
    public string? Text { get; set; } // Текст сообщения (для текстовых сообщений)

    [JsonPropertyName("photo")]
    public PhotoSize[]? Photo { get; set; } // Массив объектов PhotoSize для фотографий

    [JsonPropertyName("document")]
    public Document? Document { get; set; } // Документ (файл)

    [JsonPropertyName("audio")]
    public Audio? Audio { get; set; } // Аудиофайл

    [JsonPropertyName("video")]
    public Video? Video { get; set; } // Видеофайл

    [JsonPropertyName("voice")]
    public Voice? Voice { get; set; } // Голосовое сообщение

    [JsonPropertyName("sticker")]
    public Sticker? Sticker { get; set; } // Стикер

    [JsonPropertyName("animation")]
    public Animation? Animation { get; set; } // Анимация (GIF)

    [JsonPropertyName("contact")]
    public Contact? Contact { get; set; } // Контакт

    [JsonPropertyName("location")]
    public Location? Location { get; set; } // Местоположение

    [JsonPropertyName("venue")]
    public Venue? Venue { get; set; } // Место

    [JsonPropertyName("poll")]
    public Poll? Poll { get; set; } // Опрос

    [JsonPropertyName("new_chat_members")]
    public User[]? NewChatMembers { get; set; } // Новые участники, добавленные в чат

    [JsonPropertyName("left_chat_member")]
    public User? LeftChatMember { get; set; } // Участник, покинувший чат

    [JsonPropertyName("caption")]
    public string? Caption { get; set; } // Подпись к фото, видео, документу и т.д.

    // ... можно добавить другие поля сообщения по мере необходимости
}

// Представляет пользователя или бота Telegram
public class User
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("language_code")]
    public string? LanguageCode { get; set; }
}

// Представляет чат в Telegram
public class Chat
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "private", "group", "supergroup", "channel"

    [JsonPropertyName("title")]
    public string? Title { get; set; } // Для групп, супергрупп и каналов

    [JsonPropertyName("username")]
    public string? Username { get; set; } // Для частных чатов и каналов

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; } // Для частных чатов

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; } // Для частных чатов

    // ... можно добавить другие поля чата по мере необходимости
}

// --- Другие вспомогательные классы для различных типов обновлений ---
// Вы можете расширить их по мере необходимости, основываясь на документации Telegram Bot API.

public class PhotoSize
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("file_unique_id")]
    public string FileUniqueId { get; set; } = string.Empty;
    [JsonPropertyName("width")]
    public int Width { get; set; }
    [JsonPropertyName("height")]
    public int Height { get; set; }
    [JsonPropertyName("file_size")]
    public int? FileSize { get; set; }
}

public class Document
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("file_unique_id")]
    public string FileUniqueId { get; set; } = string.Empty;
    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
    [JsonPropertyName("file_size")]
    public int? FileSize { get; set; }
}

public class Audio
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("file_unique_id")]
    public string FileUniqueId { get; set; } = string.Empty;
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
    [JsonPropertyName("file_size")]
    public int? FileSize { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("performer")]
    public string? Performer { get; set; }
}

public class Video
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("file_unique_id")]
    public string FileUniqueId { get; set; } = string.Empty;
    [JsonPropertyName("width")]
    public int Width { get; set; }
    [JsonPropertyName("height")]
    public int Height { get; set; }
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
    [JsonPropertyName("file_size")]
    public int? FileSize { get; set; }
    [JsonPropertyName("thumbnail")]
    public PhotoSize? Thumbnail { get; set; }
}

public class Voice
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("file_unique_id")]
    public string FileUniqueId { get; set; } = string.Empty;
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
    [JsonPropertyName("file_size")]
    public int? FileSize { get; set; }
}

public class Sticker
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("file_unique_id")]
    public string FileUniqueId { get; set; } = string.Empty;
    [JsonPropertyName("width")]
    public int Width { get; set; }
    [JsonPropertyName("height")]
    public int Height { get; set; }
    [JsonPropertyName("is_animated")]
    public bool IsAnimated { get; set; }
    [JsonPropertyName("is_video")]
    public bool IsVideo { get; set; }
    [JsonPropertyName("thumbnail")]
    public PhotoSize? Thumbnail { get; set; }
    [JsonPropertyName("emoji")]
    public string? Emoji { get; set; }
    [JsonPropertyName("set_name")]
    public string? SetName { get; set; }
    [JsonPropertyName("premium_animation")]
    public File? PremiumAnimation { get; set; }
    [JsonPropertyName("mask_position")]
    public MaskPosition? MaskPosition { get; set; }
    [JsonPropertyName("file_size")]
    public int? FileSize { get; set; }
}

public class Animation
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("file_unique_id")]
    public string FileUniqueId { get; set; } = string.Empty;
    [JsonPropertyName("width")]
    public int Width { get; set; }
    [JsonPropertyName("height")]
    public int Height { get; set; }
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
    [JsonPropertyName("thumbnail")]
    public PhotoSize? Thumbnail { get; set; }
    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
    [JsonPropertyName("file_size")]
    public int? FileSize { get; set; }
}

public class Contact
{
    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }
    [JsonPropertyName("vcard")]
    public string? Vcard { get; set; }
}

public class Location
{
    [JsonPropertyName("longitude")]
    public float Longitude { get; set; }
    [JsonPropertyName("latitude")]
    public float Latitude { get; set; }
    [JsonPropertyName("horizontal_accuracy")]
    public float? HorizontalAccuracy { get; set; }
    [JsonPropertyName("live_period")]
    public int? LivePeriod { get; set; }
    [JsonPropertyName("heading")]
    public int? Heading { get; set; }
    [JsonPropertyName("proximity_alert_radius")]
    public int? ProximityAlertRadius { get; set; }
}

public class Venue
{
    [JsonPropertyName("location")]
    public Location Location { get; set; } = new Location();
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;
    [JsonPropertyName("foursquare_id")]
    public string? FoursquareId { get; set; }
    [JsonPropertyName("foursquare_type")]
    public string? FoursquareType { get; set; }
    [JsonPropertyName("google_place_id")]
    public string? GooglePlaceId { get; set; }
    [JsonPropertyName("google_place_type")]
    public string? GooglePlaceType { get; set; }
}

public class Poll
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;
    [JsonPropertyName("options")]
    public PollOption[] Options { get; set; } = Array.Empty<PollOption>();
    [JsonPropertyName("total_voter_count")]
    public int TotalVoterCount { get; set; }
    [JsonPropertyName("is_closed")]
    public bool IsClosed { get; set; }
    [JsonPropertyName("is_anonymous")]
    public bool IsAnonymous { get; set; }
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    [JsonPropertyName("allows_multiple_answers")]
    public bool AllowsMultipleAnswers { get; set; }
    [JsonPropertyName("correct_option_id")]
    public int? CorrectOptionId { get; set; }
    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }
    [JsonPropertyName("explanation_entities")]
    public MessageEntity[]? ExplanationEntities { get; set; }
    [JsonPropertyName("open_period")]
    public int? OpenPeriod { get; set; }
    [JsonPropertyName("close_date")]
    public int? CloseDate { get; set; }
}

public class PollOption
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
    [JsonPropertyName("voter_count")]
    public int VoterCount { get; set; }
}

public class MessageEntity
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    [JsonPropertyName("offset")]
    public int Offset { get; set; }
    [JsonPropertyName("length")]
    public int Length { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    [JsonPropertyName("user")]
    public User? User { get; set; }
    [JsonPropertyName("language")]
    public string? Language { get; set; }
    [JsonPropertyName("custom_emoji_id")]
    public string? CustomEmojiId { get; set; }
}

public class InlineQuery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("from")]
    public User From { get; set; } = new User();
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;
    [JsonPropertyName("offset")]
    public string Offset { get; set; } = string.Empty;
    [JsonPropertyName("chat_type")]
    public string? ChatType { get; set; }
    [JsonPropertyName("location")]
    public Location? Location { get; set; }
}

public class ChosenInlineResult
{
    [JsonPropertyName("result_id")]
    public string ResultId { get; set; } = string.Empty;
    [JsonPropertyName("from")]
    public User From { get; set; } = new User();
    [JsonPropertyName("location")]
    public Location? Location { get; set; }
    [JsonPropertyName("inline_message_id")]
    public string? InlineMessageId { get; set; }
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;
}

public class CallbackQuery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("from")]
    public User From { get; set; } = new User();
    [JsonPropertyName("message")]
    public Message? Message { get; set; }
    [JsonPropertyName("inline_message_id")]
    public string? InlineMessageId { get; set; }
    [JsonPropertyName("chat_instance")]
    public string ChatInstance { get; set; } = string.Empty;
    [JsonPropertyName("data")]
    public string? Data { get; set; }
    [JsonPropertyName("game_short_name")]
    public string? GameShortName { get; set; }
}

public class ShippingQuery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("from")]
    public User From { get; set; } = new User();
    [JsonPropertyName("invoice_payload")]
    public string InvoicePayload { get; set; } = string.Empty;
    [JsonPropertyName("shipping_address")]
    public ShippingAddress ShippingAddress { get; set; } = new ShippingAddress();
}

public class ShippingAddress
{
    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; } = string.Empty;
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;
    [JsonPropertyName("street_line1")]
    public string StreetLine1 { get; set; } = string.Empty;
    [JsonPropertyName("street_line2")]
    public string StreetLine2 { get; set; } = string.Empty;
    [JsonPropertyName("post_code")]
    public string PostCode { get; set; } = string.Empty;
}

public class PreCheckoutQuery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("from")]
    public User From { get; set; } = new User();
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;
    [JsonPropertyName("total_amount")]
    public int TotalAmount { get; set; }
    [JsonPropertyName("invoice_payload")]
    public string InvoicePayload { get; set; } = string.Empty;
    [JsonPropertyName("shipping_option_id")]
    public string? ShippingOptionId { get; set; }
    [JsonPropertyName("order_info")]
    public OrderInfo? OrderInfo { get; set; }
}

public class OrderInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    [JsonPropertyName("shipping_address")]
    public ShippingAddress? ShippingAddress { get; set; }
}

public class PollAnswer
{
    [JsonPropertyName("poll_id")]
    public string PollId { get; set; } = string.Empty;
    [JsonPropertyName("voter_chat")]
    public Chat? VoterChat { get; set; }
    [JsonPropertyName("user")]
    public User? User { get; set; }
    [JsonPropertyName("option_ids")]
    public int[] OptionIds { get; set; } = Array.Empty<int>();
}

public class ChatMemberUpdated
{
    [JsonPropertyName("chat")]
    public Chat Chat { get; set; } = new Chat();
    [JsonPropertyName("from")]
    public User From { get; set; } = new User();
    [JsonPropertyName("date")]
    public int Date { get; set; }
    [JsonPropertyName("old_chat_member")]
    public ChatMember OldChatMember { get; set; } = new ChatMember();
    [JsonPropertyName("new_chat_member")]
    public ChatMember NewChatMember { get; set; } = new ChatMember();
    [JsonPropertyName("invite_link")]
    public ChatInviteLink? InviteLink { get; set; }
}

public class ChatMember
{
    [JsonPropertyName("user")]
    public User User { get; set; } = new User();
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // "creator", "administrator", "member", "restricted", "left", "kicked"
    // ... другие поля в зависимости от статуса
}

public class ChatInviteLink
{
    [JsonPropertyName("invite_link")]
    public string InviteLink { get; set; } = string.Empty;
    [JsonPropertyName("creator")]
    public User Creator { get; set; } = new User();
    [JsonPropertyName("creates_join_request")]
    public bool CreatesJoinRequest { get; set; }
    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; set; }
    [JsonPropertyName("is_revoked")]
    public bool IsRevoked { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("expire_date")]
    public int? ExpireDate { get; set; }
    [JsonPropertyName("member_limit")]
    public int? MemberLimit { get; set; }
    [JsonPropertyName("pending_join_request_count")]
    public int? PendingJoinRequestCount { get; set; }
}

public class ChatJoinRequest
{
    [JsonPropertyName("chat")]
    public Chat Chat { get; set; } = new Chat();
    [JsonPropertyName("from")]
    public User From { get; set; } = new User();
    [JsonPropertyName("date")]
    public int Date { get; set; }
    [JsonPropertyName("bio")]
    public string? Bio { get; set; }
    [JsonPropertyName("invite_link")]
    public ChatInviteLink? InviteLink { get; set; }
}

// Вспомогательный класс для файлов (используется в PremiumAnimation)
public class File
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("file_unique_id")]
    public string FileUniqueId { get; set; } = string.Empty;
    [JsonPropertyName("file_size")]
    public int? FileSize { get; set; }
    [JsonPropertyName("file_path")]
    public string? FilePath { get; set; }
}

public class MaskPosition
{
    [JsonPropertyName("point")]
    public string Point { get; set; } = string.Empty;
    [JsonPropertyName("x_shift")]
    public float XShift { get; set; }
    [JsonPropertyName("y_shift")]
    public float YShift { get; set; }
    [JsonPropertyName("scale")]
    public float Scale { get; set; }
}
