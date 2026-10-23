using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramRetardedBot
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var botToken = "8617478637:AAFxP6Qh9M-xVWk_YR7055v--uDb47JtTj8";
            var bot = new TelegramBotClient(botToken);

            using var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            bot.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            var me = await bot.GetMe();
            Console.WriteLine($"Бот @{me.Username} успешно запущен!");
            Console.WriteLine("Нажмите Enter, чтобы остановить бота...");

            Console.ReadLine();
            cts.Cancel(); // Останавливаем бота при нажатии Enter
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Обработка текстовых команд (/start, /menu)
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                var message = update.Message;
                var chatId = message.Chat.Id;

                if (message.Text == "/start")
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "Привет! Я демо-бот. Используй /menu, чтобы открыть меню.",
                        cancellationToken: cancellationToken);
                }
                else if (message.Text == "/menu")
                {
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("👤 Профиль", "profile") },
                        new[] { InlineKeyboardButton.WithCallbackData("💰 Баланс", "balance") }
                    });

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "Выберите раздел:",
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken);
                }
            }

            // --- Обработка нажатий на inline-кнопки ---
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                var query = update.CallbackQuery;

                await botClient.AnswerCallbackQuery(
                    callbackQueryId: query.Id, 
                    cancellationToken: cancellationToken);

                var responseText = query.Data switch
                {
                    "profile" => "👤 Раздел «Профиль» пока в разработке.",
                    "balance" => "💰 Раздел «Баланс» пока в разработке.",
                    _ => "Неизвестная команда."
                };

                if (query.Message != null)
                {
                    await botClient.SendMessage(
                        chatId: query.Message.Chat.Id,
                        text: responseText,
                        cancellationToken: cancellationToken);
                }
            }
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
        {
            // Выводим ошибку в консоль
            Console.WriteLine($"Ошибка [{source}]: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}