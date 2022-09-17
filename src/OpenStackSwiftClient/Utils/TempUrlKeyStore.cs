using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpenStackSwiftClient.Utils
{
  class TempUrlKeyStore
  {
    TempUrlKey _key;

    public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);

    public void SetKey(TempUrlKey key) {
      _key = key;
    }

    public TempUrlKey GetKey() {
      return _key;
    }
  }

  class TempUrlKey
  {
    public TempUrlKey(string Key, DateTime? dateCreated) {
      this.Key = Key;
      DateCreated = dateCreated;
    }

    public string Key { get; }
    public DateTime? DateCreated { get; }
  }
}