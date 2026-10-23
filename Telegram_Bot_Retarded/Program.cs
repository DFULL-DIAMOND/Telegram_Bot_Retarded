using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotDemo
{
    internal class Program
    {
        // Хранилище балансов пользователей (ChatId -> Баланс)
        private static readonly ConcurrentDictionary<long, decimal> UserBalances = new();

        // Хранилище состояний пользователей (ChatId -> Текущее действие)
        private static readonly ConcurrentDictionary<long, string> UserStates = new();

        static async Task Main(string[] args)
        {
            var bot = new TelegramBotClient("8617478637:AAFxP6Qh9M-xVWk_YR7055v--uDb47JtTj8");
            using var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);

            var me = await bot.GetMe();
            Console.WriteLine($"Бот @{me.Username} запущен!");
            Console.ReadLine();
            cts.Cancel();
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
            if (chatId == null) return;

            // ==========================================
            // 1. ОБРАБОТКА ТЕКСТА (Ввод суммы)
            // ==========================================
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                var text = update.Message.Text;

                // Если бот ждет ввода суммы от этого пользователя
                if (UserStates.TryGetValue(chatId.Value, out var state))
                {
                    // Пытаемся превратить текст в число
                    if (decimal.TryParse(text, out var amount) && amount > 0)
                    {
                        // Создаем клавиатуру для быстрого доступа
                        var quickKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("➕ Положить", "deposit") },
                            new[] { InlineKeyboardButton.WithCallbackData("➖ Снять", "withdraw") },
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ В меню", "menu") } // Новая кнопка
                        });

                        if (state == "awaiting_deposit")
                        {
                            // Пополняем баланс
                            UserBalances.AddOrUpdate(chatId.Value, amount, (key, oldValue) => oldValue + amount);

                            // Получаем актуальный баланс для вывода
                            var currentBalance = UserBalances[chatId.Value];

                            await botClient.SendMessage(
                                chatId.Value,
                                $"✅ Счет пополнен на {amount} руб.\n\n💰 Ваш текущий баланс: {currentBalance} руб.",
                                replyMarkup: quickKeyboard,
                                cancellationToken: cancellationToken);
                        }
                        else if (state == "awaiting_withdraw")
                        {
                            var currentBalance = UserBalances.GetOrAdd(chatId.Value, 0);
                            if (currentBalance >= amount)
                            {
                                // Снимаем деньги
                                UserBalances.AddOrUpdate(chatId.Value, 0, (key, oldValue) => oldValue - amount);

                                // Получаем актуальный баланс для вывода
                                var newBalance = UserBalances[chatId.Value];

                                await botClient.SendMessage(
                                    chatId.Value,
                                    $"✅ Списано {amount} руб.\n\n💰 Ваш текущий баланс: {newBalance} руб.",
                                    replyMarkup: quickKeyboard,
                                    cancellationToken: cancellationToken);
                            }
                            else
                            {
                                await botClient.SendMessage(
                                    chatId.Value,
                                    $"❌ Недостаточно средств! Ваш баланс: {currentBalance} руб.",
                                    replyMarkup: quickKeyboard,
                                    cancellationToken: cancellationToken);
                            }
                        }

                        // Сбрасываем состояние (ввод завершен)
                        UserStates.TryRemove(chatId.Value, out _);
                        return; // Выходим, чтобы не обрабатывать это как команду
                    }
                    else
                    {
                        await botClient.SendMessage(chatId.Value, "⚠️ Ошибка! Введите корректное положительное число (например: 500).", cancellationToken: cancellationToken);
                        return;
                    }
                }

                // Обработка стандартных команд
                if (text == "/start")
                {
                    await botClient.SendMessage(chatId.Value, "Привет! Используй /menu.", cancellationToken: cancellationToken);
                }
                else if (text == "/menu")
                {
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("👤 Профиль", "profile") },
                        new[] { InlineKeyboardButton.WithCallbackData("💰 Баланс", "balance") }
                    });
                    await botClient.SendMessage(chatId.Value, "Выберите раздел:", replyMarkup: keyboard, cancellationToken: cancellationToken);
                }
            }

            // ==========================================
            // 2. ОБРАБОТКА НАЖАТИЙ НА КНОПКИ
            // ==========================================
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is { } query)
            {
                await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
                var data = query.Data;

                if (data == "profile")
                {
                    await botClient.SendMessage(chatId.Value, "👤 Раздел «Профиль» пока в разработке.", cancellationToken: cancellationToken);
                }
                else if (data == "balance")
                {
                    var balance = UserBalances.GetOrAdd(chatId.Value, 0);
                    var balanceKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("➕ Положить", "deposit") },
                        new[] { InlineKeyboardButton.WithCallbackData("➖ Снять", "withdraw") },
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ В меню", "menu") } // Новая кнопка
                    });
                    await botClient.SendMessage(chatId.Value, $"💰 Ваш баланс: {balance} руб.\nВыберите действие:", replyMarkup: balanceKeyboard, cancellationToken: cancellationToken);
                }
                else if (data == "deposit")
                {
                    UserStates[chatId.Value] = "awaiting_deposit"; // Запоминаем, что ждем сумму
                    await botClient.SendMessage(chatId.Value, "Введите сумму для пополнения (в рублях):", cancellationToken: cancellationToken);
                }
                else if (data == "withdraw")
                {
                    UserStates[chatId.Value] = "awaiting_withdraw"; // Запоминаем, что ждем сумму
                    await botClient.SendMessage(chatId.Value, "Введите сумму для снятия (в рублях):", cancellationToken: cancellationToken);
                }
                else if (data == "menu")
                {
                    // Возвращаем пользователя в главное меню
                    var mainKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("👤 Профиль", "profile") },
                        new[] { InlineKeyboardButton.WithCallbackData("💰 Баланс", "balance") }
                    });
                    await botClient.SendMessage(chatId.Value, "Главное меню:", replyMarkup: mainKeyboard, cancellationToken: cancellationToken);
                }
            }
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка [{source}]: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}