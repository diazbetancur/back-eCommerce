using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace CC.Domain.Helpers
{
    public static class PasswordExtensions
    {
        /// <summary>
        /// Generates a Random Password
        /// respecting the given strength requirements.
        /// </summary>
        /// <param name="opts">A valid PasswordOptions object
        /// containing the password strength requirements.</param>
        /// <returns>A random password</returns>
        public static string GenerateRandomPassword(PasswordOptions opts = null)
        {
            if (opts == null)
            {
                opts = new PasswordOptions
                {
                    RequiredLength = 8,
                    RequireDigit = true,
                    RequireLowercase = true,
                    RequireUppercase = true,
                    RequireNonAlphanumeric = true,
                    RequiredUniqueChars = 1
                };
            }

            string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string lower = "abcdefghijklmnopqrstuvwxyz";
            string digit = "1234567890";
            string nonAlphanumeric = "!@#$%^&*()_-+=[{]};:>|./?";

            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            List<char> chars = new List<char>();

            // Garantizar inclusión de las reglas mínimas
            if (opts.RequireUppercase) chars.Add(upper[GetRandomInt(rng, upper.Length)]);
            if (opts.RequireLowercase) chars.Add(lower[GetRandomInt(rng, lower.Length)]);
            if (opts.RequireDigit) chars.Add(digit[GetRandomInt(rng, digit.Length)]);
            if (opts.RequireNonAlphanumeric) chars.Add(nonAlphanumeric[GetRandomInt(rng, nonAlphanumeric.Length)]);

            // Completar el resto hasta alcanzar la longitud requerida
            string allChars = $"{upper}{lower}{digit}{nonAlphanumeric}";
            while (chars.Count < opts.RequiredLength || chars.Distinct().Count() < opts.RequiredUniqueChars)
            {
                chars.Add(allChars[GetRandomInt(rng, allChars.Length)]);
            }

            // Mezclar los caracteres para que no sea siempre el mismo orden
            return new string(chars.OrderBy(x => GetRandomInt(rng, int.MaxValue)).ToArray());
        }

        private static int GetRandomInt(RandomNumberGenerator rng, int max)
        {
            byte[] randomBytes = new byte[4];
            rng.GetBytes(randomBytes);
            int value = BitConverter.ToInt32(randomBytes, 0) & int.MaxValue;
            return value % max;
        }

        public const string USER_NOT_FOUND = "User Not Found";
        public const string USER_PASWORD_INCORRECT = "User pasword incorect";

        public static string GetSHA256(this string str)
        {
            SHA256 sha256 = SHA256.Create();
            ASCIIEncoding encoding = new();
            StringBuilder sb = new();
            byte[] stream = sha256.ComputeHash(encoding.GetBytes(str));
            for (int i = 0; i < stream.Length; i++) sb.AppendFormat("{0:x2}", stream[i]);
            return sb.ToString();
        }
    }
}