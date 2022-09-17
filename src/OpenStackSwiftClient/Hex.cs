using System;
using System.Linq;
using System.Text;

namespace OpenStackSwiftClient
{
  public static class Hex
  {
    public static bool IsHex(char ch) {
      if (ch >= '0' && ch <= '9')
        return true;
      if (ch >= 'a' && ch <= 'f')
        return true;
      if (ch >= 'A' && ch <= 'F')
        return true;
      return false;
    }

    public static bool IsHex(string text) {
      return text.All(IsHex);
    }

    public static int FromHex(char ch) {
      if (ch >= '0' && ch <= '9')
        return ch - '0';
      if (ch >= 'a' && ch <= 'f')
        return ch - 'a' + 10;
      if (ch >= 'A' && ch <= 'F')
        return ch - 'A' + 10;
      throw new ArgumentException("The char is not a hexadecimal digit", nameof(ch));
    }

    public static byte[] FromHex(string text) {
      if (text == null)
        throw new ArgumentNullException(nameof(text));
      if (text.Length % 2 != 0)
        throw new ArgumentException("The text must have an even number of characters.");

      var data = new byte[text.Length / 2];

      for (var i = 0; i < data.GetLength(0); i++)
        data[i] = (byte)(FromHex(text[i * 2 + 1]) + 16 * FromHex(text[i * 2]));

      return data;
    }

    public static string ToHex(byte[] data) {
      var sb = new StringBuilder(data.GetLength(0) * 2 + 2);

      for (var i = 0; i < data.GetLength(0); i++)
        sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:x2}", data[i]);

      return sb.ToString();
    }
  }
}
