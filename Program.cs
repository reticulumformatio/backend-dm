using TelegramJsons;


var builder = WebApplication.CreateBuilder(args);
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";


builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
            {
                policy.WithOrigins("https://dmunlimited.online")
                  .SetIsOriginAllowedToAllowWildcardSubdomains()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            });
});
builder.Services.AddHttpClient();
builder.Services.AddScoped<TelegramUpdates>();

var app = builder.Build();

app.UseCors(MyAllowSpecificOrigins);
app.UseRouting();



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
app.MapPost("/api/telegram-updates", async (Update update, TelegramUpdates handler) =>
{
    return await handler.OnTelegramUpdateAsync(update);
});


app.Run();




