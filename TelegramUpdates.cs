using TelegramJsons;

class TelegramUpdates{
    private ILogger<Program> _logger;
    private IHttpClientFactory _httpClientFactory;
    private IConfiguration _configuration;

    private readonly string _token;
    private readonly string _telegramApiUrl;

    public TelegramUpdates(ILogger<Program> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration){
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _token = _configuration["TelegramSettings:BotToken"] ?? throw new InvalidOperationException("Telegram Bot Token не настроен в конфигурации.");
        _telegramApiUrl = $"https://api.telegram.org/bot{_token}/sendMessage";
    }


    public async Task<IResult> OnTelegramUpdateAsync(Update update)
    {
        if (update == null)
        {
            _logger.LogWarning("Получено пустое или невалидное обновление вебхука Telegram.");
            return Results.BadRequest("Invalid Telegram update.");
        }

        _logger.LogInformation("Получено обновление вебхука Telegram. ID Обновления: {UpdateId}, Тип: {UpdateType}", update.UpdateId, update.Type);

        // Пример обработки текстового сообщения
        if (update.Message != null)
        {
            long chatId = update.Message.Chat!.Id;
            Message message = update.Message;

            _logger.LogInformation("Сообщение из чата {ChatId}: {UserMessage}", chatId, message);
            _logger.LogInformation("Сообщение отправлено пользователем: {UserName} (ID: {UserId})", update.Message.From?.Username ?? update.Message.From?.FirstName, update.Message.From?.Id);

            // Вызываем приватный метод для обработки AI и отправки ответа обратно в Telegram
            await OnMessageUpdate(chatId, message);
        }
        else if (update.CallbackQuery != null)
        {
            long chatId = update.CallbackQuery.Message?.Chat?.Id ?? update.CallbackQuery.From?.Id ?? 0;
            string callbackData = update.CallbackQuery.Data ?? "Нет данных";
            _logger.LogInformation("Получен Callback Query от пользователя ID: {UserId} с данными: {CallbackData}", update.CallbackQuery.From?.Id, callbackData);
        }
        else
        {
            _logger.LogInformation("Получен необработанный тип обновления Telegram: {UpdateType}", update.Type);
        }

        // Telegram ожидает HTTP 200 OK в ответ на вебхук.
        return Results.Ok();
    }

    private async Task SendMessage(long chatId, string MessageText)
    {
        // Подготовка данных для отправки в Telegram API
        var telegramPayload = new
        {
            chat_id = chatId,
            text = MessageText,
            parse_mode = "HTML" // Можно использовать MarkdownV2 или HTML для форматирования
        };

        try
        {
            var response = await _httpClientFactory!.CreateClient().PostAsJsonAsync(_telegramApiUrl, telegramPayload);

            if (response.IsSuccessStatusCode)
            {
                _logger!.LogInformation("Message successfully sent to Telegram.");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger!.LogError("Failed to send message to Telegram. Status: {StatusCode}, Content: {ErrorContent}", response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger!.LogError(ex, "An error occurred while sending message to Telegram.");
        }
    }

    private async Task OnMessageUpdate(long chatId, Message message){
        if(message.Text == null) return;
        string messageText = message.Text;
        await SendMessage(chatId, messageText);
    }
}