using System;
using System.Security.Cryptography;
using System.Text;

namespace SystemMonitor
{
    public static class ConnectionCodeGenerator
    {
        private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        private static readonly char[] _codeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
        
        public static string GenerateCode()
        {
            byte[] randomBytes = new byte[4];
            _rng.GetBytes(randomBytes);
            
            uint randomNumber = BitConverter.ToUInt32(randomBytes, 0);
            
            // Создаем 6-значный код
            StringBuilder code = new StringBuilder(6);
            for (int i = 0; i < 6; i++)
            {
                int index = (int)(randomNumber % _codeChars.Length);
                code.Append(_codeChars[index]);
                randomNumber /= (uint)_codeChars.Length;
            }
            
            // Форматируем как XXX-XXX
            return $"{code.ToString(0, 3)}-{code.ToString(3, 3)}";
        }
        
        public static bool ValidateCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != 7)
                return false;
            
            // Проверяем формат XXX-XXX
            if (code[3] != '-')
                return false;
            
            // Убираем дефис для проверки символов
            string cleanCode = code.Replace("-", "");
            
            foreach (char c in cleanCode)
            {
                if (Array.IndexOf(_codeChars, c) == -1)
                    return false;
            }
            
            return true;
        }
    }
}