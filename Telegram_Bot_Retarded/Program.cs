using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram_Bot_Retarded; // Ваше пространство имен для AppDbContext и BotUser

namespace TelegramBotDemo
{
    internal class Program
    {
        private static readonly ConcurrentDictionary<long, string> UserStates = new();

        static async Task Main(string[] args)
        {
            // Инициализация БД
            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();
            }

            // ВАЖНО: Вставьте сюда ваш токен
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
            // 1. ОБРАБОТКА ТЕКСТА (Ввод суммы и команды)
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
                        var quickKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("➕ Положить", "deposit") },
                            new[] { InlineKeyboardButton.WithCallbackData("➖ Снять", "withdraw") },
                            new[] { InlineKeyboardButton.WithCallbackData("⬅️ В меню", "menu") }
                        });

                        if (state == "awaiting_deposit")
                        {
                            using var db = new AppDbContext();
                            var user = db.Users.FirstOrDefault(u => u.Id == chatId.Value);
                            if (user == null)
                            {
                                user = new BotUser { Id = chatId.Value, Balance = 0 };
                                db.Users.Add(user);
                            }

                            user.Balance += amount;
                            db.SaveChanges();

                            await botClient.SendMessage(chatId.Value,
                                $"✅ Счет пополнен на {amount} руб.\n\n💰 Ваш текущий баланс: {user.Balance} руб.",
                                replyMarkup: quickKeyboard, cancellationToken: cancellationToken);
                        }
                        else if (state == "awaiting_withdraw")
                        {
                            using var db = new AppDbContext();
                            var user = db.Users.FirstOrDefault(u => u.Id == chatId.Value);
                            if (user == null)
                            {
                                user = new BotUser { Id = chatId.Value, Balance = 0 };
                                db.Users.Add(user);
                                db.SaveChanges();
                            }

                            if (user.Balance >= amount)
                            {
                                user.Balance -= amount;
                                db.SaveChanges();

                                await botClient.SendMessage(chatId.Value,
                                    $"✅ Списано {amount} руб.\n\n💰 Ваш текущий баланс: {user.Balance} руб.",
                                    replyMarkup: quickKeyboard, cancellationToken: cancellationToken);
                            }
                            else
                            {
                                await botClient.SendMessage(chatId.Value,
                                    $"❌ Недостаточно средств! Ваш баланс: {user.Balance} руб.",
                                    replyMarkup: quickKeyboard, cancellationToken: cancellationToken);
                            }
                        }

                        UserStates.TryRemove(chatId.Value, out _);
                        return;
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
                    var fromUser = update.Message.From;
                    var displayName = fromUser?.Username != null
                        ? $"@{fromUser.Username}"
                        : (fromUser?.FirstName ?? "друг");

                    var startText = $"🚀 *Добро пожаловать, {displayName}!*\n\n" +
                                    $"Этот бот создан для демонстрации работы с базами данных и обработки состояний.\n\n" +
                                    $"Нажми /menu или кнопку ниже, чтобы открыть главное меню.";

                    var replyKeyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "📋 Главное меню" }
                    })
                    {
                        ResizeKeyboard = true,
                        IsPersistent = true
                    };

                    await botClient.SendMessage(
                        chatId.Value,
                        startText,
                        replyMarkup: replyKeyboard,
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken);
                }
                else if (text == "/menu" || text == "📋 Главное меню")
                {
                    // Передаем имя из текстового сообщения
                    await ShowMainMenu(botClient, chatId.Value, update.Message.From?.FirstName, cancellationToken);
                }
            }

            // ==========================================
            // 2. ОБРАБОТКА НАЖАТИЙ НА INLINE-КНОПКИ
            // ==========================================
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is { } query)
            {
                try
                {
                    await botClient.AnswerCallbackQuery(query.Id, cancellationToken: cancellationToken);
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                {
                    if (!ex.Message.Contains("query is too old"))
                    {
                        Console.WriteLine($"Ошибка при AnswerCallbackQuery: {ex.Message}");
                    }
                }

                var data = query.Data;

                if (data == "profile")
                {
                    using var db = new AppDbContext();
                    var user = db.Users.FirstOrDefault(u => u.Id == chatId.Value);
                    var balance = user?.Balance ?? 0;

                    var tgUser = query.From;
                    var firstName = tgUser.FirstName;
                    var username = tgUser.Username != null ? $"@{tgUser.Username}" : "не указан";

                    var profileText = $"👤 **Ваш профиль**\n\n" +
                                      $"🆔 ID: `{chatId.Value}`\n" +
                                      $"📛 Имя: {firstName}\n" +
                                      $"🔗 Username: {username}\n" +
                                      $"💰 Баланс: {balance} руб.";

                    var profileKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ В меню", "menu") }
                    });

                    await botClient.SendMessage(
                        chatId.Value,
                        profileText,
                        replyMarkup: profileKeyboard,
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken);
                }
                else if (data == "balance")
                {
                    using var db = new AppDbContext();
                    var user = db.Users.FirstOrDefault(u => u.Id == chatId.Value);
                    var balance = user?.Balance ?? 0;

                    var balanceText = $"💰 *Твой кошелек*\n\n" +
                                      $"Доступно средств: *{balance} руб.*\n\n" +
                                      $"Что хочешь сделать? 👇";

                    var balanceKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("➕ Положить", "deposit") },
                        new[] { InlineKeyboardButton.WithCallbackData("➖ Снять", "withdraw") },
                        new[] { InlineKeyboardButton.WithCallbackData("⬅️ В меню", "menu") }
                    });

                    await botClient.SendMessage(
                        chatId.Value,
                        balanceText,
                        replyMarkup: balanceKeyboard,
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken);
                }
                else if (data == "deposit")
                {
                    UserStates[chatId.Value] = "awaiting_deposit";
                    await botClient.SendMessage(chatId.Value, "Введите сумму для пополнения (в рублях):", cancellationToken: cancellationToken);
                }
                else if (data == "withdraw")
                {
                    UserStates[chatId.Value] = "awaiting_withdraw";
                    await botClient.SendMessage(chatId.Value, "Введите сумму для снятия (в рублях):", cancellationToken: cancellationToken);
                }
                else if (data == "menu")
                {
                    // ИСПРАВЛЕНО: при клике на кнопку берем имя из query.From, а не из update.Message
                    await ShowMainMenu(botClient, chatId.Value, query.From?.FirstName, cancellationToken);
                }
            }
        }

        // ==========================================
        // 3. ВЫНЕСЕННЫЙ МЕТОД ДЛЯ ОТОБРАЖЕНИЯ МЕНЮ
        // ==========================================
        private static async Task ShowMainMenu(ITelegramBotClient botClient, long chatId, string? firstName, CancellationToken ct)
        {
            using var db = new AppDbContext();
            var user = db.Users.FirstOrDefault(u => u.Id == chatId);
            var balance = user?.Balance ?? 0;
            var displayName = firstName ?? "друг";

            var menuText = $"👋 Привет, *{displayName}*!\n\n" +
                           $"Я твой финансовый помощник.\n\n" +
                           $"💳 Твой текущий баланс: *{balance} руб.*\n\n" +
                           $"Выбери действие на клавиатуре ниже 👇";

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("👤 Профиль", "profile") },
                new[] { InlineKeyboardButton.WithCallbackData("💰 Баланс", "balance") }
            });

            await botClient.SendMessage(
                chatId: chatId,
                text: menuText,
                replyMarkup: keyboard,
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка [{source}]: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}