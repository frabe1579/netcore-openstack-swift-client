using System.Security.Cryptography;

namespace OpenStackSwiftClient.UnitTests
{
  public static class Hash
  {
    public static HashAlgorithm CreateAlgorithm(HashMode mode) {
      switch (mode) {
        case HashMode.SHA1:
          return SHA1.Create();
        //return new SHA1CryptoServiceProvider();
        case HashMode.SHA256:
          return SHA256.Create(); ;
        //return new SHA256CryptoServiceProvider();
        case HashMode.MD5:
          return MD5.Create();
        default:
          throw new ArgumentException();
      }
    }

    public static byte[] ComputeHashFromFile(string path, HashMode mode) {
      if (path == null)
        throw new ArgumentNullException(nameof(path));

      var fs = File.OpenRead(path);

      try {
        return ComputeHash(fs, mode);
      }
      finally {
        fs.Dispose();
      }
    }

    public static byte[] ComputeHash(byte[] data, HashMode mode) {
      if (data == null)
        throw new ArgumentNullException(nameof(data));

      var alg = CreateAlgorithm(mode);
      byte[] hash;

      try {
        hash = alg.ComputeHash(data);
      }
      finally {
        alg.Dispose();
      }

      return hash;
    }

    public static byte[] ComputeHash(byte[] data, int offset, int count, HashMode mode) {
      if (data == null)
        throw new ArgumentNullException(nameof(data));

      var alg = CreateAlgorithm(mode);
      byte[] hash;

      try {
        hash = alg.ComputeHash(data, offset, count);
      }
      finally {
        alg.Dispose();
      }

      return hash;
    }

    public static byte[] ComputeHash(Stream data, HashMode mode) {
      if (data == null)
        throw new ArgumentNullException(nameof(data));

      var alg = CreateAlgorithm(mode);
      byte[] hash;

      try {
        hash = alg.ComputeHash(data);
      }
      finally {
        alg.Dispose();
      }

      return hash;
    }

    public static async Task<string> ComputeHashAsync(Stream data, HashMode mode, CancellationToken cancellationToken) {
      if (data == null)
        throw new ArgumentNullException(nameof(data));

      using (var hashStream = HashStream.CreateRead(data, mode)) {
        await hashStream.CopyToAsync(Stream.Null, 81920, cancellationToken);
        return hashStream.ComputeHash();
      }
    }

    public static async Task<string> ComputeHashFromFileAsync(string filePath, HashMode mode, CancellationToken cancellationToken) {
      using (var stm = File.OpenRead(filePath))
        return await ComputeHashAsync(stm, mode, cancellationToken);
    }
  }

  public enum HashMode
  {
    SHA1,
    SHA256,
    MD5,
  }
}