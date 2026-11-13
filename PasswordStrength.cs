using System;
using System.Collections.Generic;
using System.Linq;

namespace TremauxLock
{
    internal enum PasswordStrengthLevel
    {
        VeryWeak,
        Weak,
        Medium,
        Strong
    }

    internal static class PasswordStrength
    {
        private const int MinimumSequentialRun = 5;

        private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
        {
            "1234567890",
            "password",
            "password1",
            "password12",
            "password123",
            "qwertyuiop",
            "senha123",
            "senha1234",
            "senha12345",
            "senha123456",
            "tremauxlock",
            "admin12345"
        };

        public static PasswordStrengthLevel Evaluate(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return PasswordStrengthLevel.VeryWeak;
            }

            if (IsRepeatedCharacter(password)
                || HasSequentialRun(password)
                || CommonPasswords.Contains(password))
            {
                return PasswordStrengthLevel.VeryWeak;
            }

            int characterClasses = CountCharacterClasses(password);
            int score = 0;

            if (password.Length >= 10)
            {
                score++;
            }

            if (password.Length >= 14)
            {
                score++;
            }

            if (characterClasses >= 2)
            {
                score++;
            }

            if (characterClasses >= 3)
            {
                score++;
            }

            if (characterClasses >= 4)
            {
                score++;
            }

            int distinctCharacters = password.Distinct().Count();
            if (distinctCharacters >= Math.Max(6, password.Length / 2))
            {
                score++;
            }

            return score switch
            {
                <= 1 => PasswordStrengthLevel.VeryWeak,
                2 => PasswordStrengthLevel.Weak,
                3 or 4 => PasswordStrengthLevel.Medium,
                _ => PasswordStrengthLevel.Strong
            };
        }

        public static string GetLabel(PasswordStrengthLevel level) => level switch
        {
            PasswordStrengthLevel.VeryWeak => "Muito fraca",
            PasswordStrengthLevel.Weak => "Fraca",
            PasswordStrengthLevel.Medium => "Média",
            _ => "Forte"
        };

        private static int CountCharacterClasses(string password)
        {
            bool hasLower = false;
            bool hasUpper = false;
            bool hasDigit = false;
            bool hasSymbol = false;

            foreach (char character in password)
            {
                if (char.IsLower(character))
                {
                    hasLower = true;
                }
                else if (char.IsUpper(character))
                {
                    hasUpper = true;
                }
                else if (char.IsDigit(character))
                {
                    hasDigit = true;
                }
                else
                {
                    hasSymbol = true;
                }
            }

            return (hasLower ? 1 : 0)
                + (hasUpper ? 1 : 0)
                + (hasDigit ? 1 : 0)
                + (hasSymbol ? 1 : 0);
        }

        private static bool IsRepeatedCharacter(string password) =>
            password.Distinct().Count() == 1;

        private static bool HasSequentialRun(string password)
        {
            int ascending = 1;
            int descending = 1;

            for (int index = 1; index < password.Length; index++)
            {
                int difference = password[index] - password[index - 1];
                ascending = difference == 1 ? ascending + 1 : 1;
                descending = difference == -1 ? descending + 1 : 1;

                if (ascending >= MinimumSequentialRun || descending >= MinimumSequentialRun)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
