using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TelJel;

/// <summary>
/// Sends messages to the Telegram Bot API.
/// </summary>
public sealed class TelegramSender : IDisposable
{
    private const int MaxCaptionLength = 1024;
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramSender"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public TelegramSender(ILogger<TelegramSender> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient.Dispose();
    }

    /// <summary>
    /// Sends a text message, optionally with a photo.
    /// </summary>
    /// <param name="botToken">Bot token.</param>
    /// <param name="chatId">Chat id.</param>
    /// <param name="message">HTML message body.</param>
    /// <param name="imageUrl">Optional image URL.</param>
    /// <param name="silent">Whether to disable notification sound.</param>
    /// <param name="threadId">Optional forum thread id.</param>
    /// <returns>True when Telegram accepted the request.</returns>
    public async Task<bool> SendAsync(
        string botToken,
        string chatId,
        string message,
        string? imageUrl,
        bool silent,
        string? threadId)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogWarning("TelJel: missing bot token or chat id");
            return false;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                if (message.Length > MaxCaptionLength)
                {
                    var shortCaption = message[..900] + "\n\n[Details below]";
                    var photoOk = await SendPhotoAsync(botToken, chatId, shortCaption, imageUrl, silent, threadId).ConfigureAwait(false);
                    var textOk = await SendTextAsync(botToken, chatId, message, silent, threadId).ConfigureAwait(false);
                    return photoOk && textOk;
                }

                return await SendPhotoAsync(botToken, chatId, message, imageUrl, silent, threadId).ConfigureAwait(false);
            }

            return await SendTextAsync(botToken, chatId, message, silent, threadId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TelJel: failed to send Telegram message to {ChatId}", chatId);
            return false;
        }
    }

    private async Task<bool> SendTextAsync(string botToken, string chatId, string message, bool silent, string? threadId)
    {
        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
        var parameters = new Dictionary<string, string>
        {
            ["chat_id"] = chatId,
            ["text"] = message,
            ["parse_mode"] = "HTML",
            ["disable_web_page_preview"] = "true"
        };

        if (silent)
        {
            parameters["disable_notification"] = "true";
        }

        if (!string.IsNullOrWhiteSpace(threadId))
        {
            parameters["message_thread_id"] = threadId;
        }

        using var content = new FormUrlEncodedContent(parameters);
        using var response = await _httpClient.PostAsync(url, content).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogError("TelJel: sendMessage failed ({Status}): {Body}", response.StatusCode, body);
            return false;
        }

        _logger.LogInformation("TelJel: message sent to {ChatId}", chatId);
        return true;
    }

    private async Task<bool> SendPhotoAsync(
        string botToken,
        string chatId,
        string caption,
        string imageUrl,
        bool silent,
        string? threadId)
    {
        var url = $"https://api.telegram.org/bot{botToken}/sendPhoto";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId), "chat_id");
        form.Add(new StringContent(caption), "caption");
        form.Add(new StringContent("HTML"), "parse_mode");
        form.Add(new StringContent(silent ? "true" : "false"), "disable_notification");

        if (!string.IsNullOrWhiteSpace(threadId))
        {
            form.Add(new StringContent(threadId), "message_thread_id");
        }

        using var imageResponse = await _httpClient.GetAsync(imageUrl).ConfigureAwait(false);
        if (!imageResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("TelJel: could not download poster {ImageUrl}, falling back to text", imageUrl);
            return await SendTextAsync(botToken, chatId, caption, silent, threadId).ConfigureAwait(false);
        }

        var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imageContent = new ByteArrayContent(imageBytes);
        form.Add(imageContent, "photo", "poster.jpg");

        using var response = await _httpClient.PostAsync(url, form).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogError("TelJel: sendPhoto failed ({Status}): {Body}", response.StatusCode, body);
            return await SendTextAsync(botToken, chatId, caption, silent, threadId).ConfigureAwait(false);
        }

        _logger.LogInformation("TelJel: photo message sent to {ChatId}", chatId);
        return true;
    }
}
