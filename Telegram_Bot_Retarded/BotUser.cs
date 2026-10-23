using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Telegram_Bot_Retarded
{
    public class BotUser
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // <-- Добавить эту строку
        public long Id { get; set; } // Telegram ChatId (первичный ключ)
        public decimal Balance { get; set; } // Баланс пользователя6
    }
}
