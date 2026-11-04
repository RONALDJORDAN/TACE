using System;
using System.Linq;

namespace TACEUI.Core
{
    public static class PatrimonioHelper
    {
        public const string Prefixo = "INV";

        public static bool TryNormalizar(string entrada, out string normalizado, out string erro)
        {
            normalizado = string.Empty;
            erro = string.Empty;

            string valor = (entrada ?? string.Empty).Trim();
            if (valor.Length == 0) return true;

            if (valor.StartsWith(Prefixo, StringComparison.OrdinalIgnoreCase))
                valor = valor.Substring(Prefixo.Length).Trim();

            valor = valor.Replace(" ", string.Empty);
            if (valor.Length == 0)
            {
                erro = $"Informe os números do patrimônio. Exemplo: {Prefixo}0425.";
                return false;
            }

            if (!valor.All(char.IsDigit))
            {
                erro = $"Digite apenas números. O prefixo {Prefixo} é automático. Exemplo: 425 -> {Prefixo}425.";
                return false;
            }

            normalizado = Prefixo + valor;
            return true;
        }

        public static string NormalizarOuOriginal(string entrada)
        {
            return TryNormalizar(entrada, out string normalizado, out _)
                ? normalizado
                : (entrada ?? string.Empty).Trim();
        }

        public static long ExtrairNumero(string entrada)
        {
            if (string.IsNullOrWhiteSpace(entrada)) return long.MaxValue;

            string digitos = new string((entrada ?? string.Empty).Where(char.IsDigit).ToArray());
            if (long.TryParse(digitos, out long numero)) return numero;
            return long.MaxValue - 1;
        }

        /// <summary>
        /// Extrai o numero do patrimonio para uso como ID alinhado.
        /// Retorna 0 quando nao ha um numero valido maior que zero.
        /// </summary>
        public static int ExtrairIdNumerico(string entrada)
        {
            if (string.IsNullOrWhiteSpace(entrada)) return 0;

            string digitos = new string((entrada ?? string.Empty).Where(char.IsDigit).ToArray());
            return int.TryParse(digitos, out int numero) && numero > 0 ? numero : 0;
        }
    }
}
