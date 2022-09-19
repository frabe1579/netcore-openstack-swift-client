using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenStackSwiftClient.Models;
using OpenStackSwiftClient.UnitTests.TestUtils;

namespace OpenStackSwiftClient.UnitTests
{
  public class ClientTests
  {
    const string TestContainerPrefix = "test-delete-me-swiftClient-tests-";

    ServiceProvider CreateProvider() {
      var services = new ServiceCollection();

      var cb = new ConfigurationBuilder()
        .AddJsonFile("./settings.json")
        .AddUserSecrets(typeof(ClientTests).Assembly)
        .AddEnvironmentVariables("TEST_");
      var config = cb.Build();
      var options = new OpenStackOptions();
      config.GetSection("openstack").Bind(options);

      services.AddHttpClient();

      services.AddOpenStackSwiftClient(o => {
        o.AuthUrl = options.AuthUrl;
        o.Username = options.Username;
        o.UserDomainName = options.UserDomainName;
        o.Password = options.Password;
        o.ProjectId = options.ProjectId;
        o.ProjectName = options.ProjectName;
        o.ProjectDomainName = options.ProjectDomainName;
        o.RegionName = options.RegionName;
      });

      return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TestContainer_Found() {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");
        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        var info = await client.GetContainerInfoAsync(containerName).ConfigureAwait(false);
        Assert.NotNull(info);
        Assert.Equal(0L, info.ObjectCount);
        Assert.Equal(0L, info.BytesUsed);
        await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
      }
    }

    [Fact]
    public async Task TestContainer_NotFound_ReturnNull() {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");
        var info = await client.GetContainerInfoAsync(containerName).ConfigureAwait(false);
        Assert.Null(info);
      }
    }

    [Fact]
    public async Task TestOverwritFalse() {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");
        var objectName = Guid.NewGuid().ToString("N");
        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        using (var randomStream = new RandomStream(1000))
          await client.UploadObjectAsync(containerName, objectName, randomStream, overwrite: false).ConfigureAwait(false);
        using (var randomStream = new RandomStream(1000))
          await Assert.ThrowsAsync<ObjectAlreadyExistsException>(() => client.UploadObjectAsync(containerName, objectName, randomStream, overwrite: false)).ConfigureAwait(false);
      }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public async Task TestObject(int length) {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");
        var objectName = Guid.NewGuid().ToString("N");
        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        ObjectInfoModel sourceInfo;
        using (var randomStream = new RandomStream(length))
          sourceInfo = await client.UploadObjectAsync(containerName, objectName, new SourceStreamWithUnknownLength(randomStream)).ConfigureAwait(false);
        Assert.NotNull(sourceInfo);
        Assert.NotNull(sourceInfo.Hash);
        Assert.Null(sourceInfo.Bytes);
        Assert.Equal(objectName, sourceInfo.Name);
        Assert.True(length == 0 || sourceInfo.Hash != Hex.ToHex(Hash.ComputeHash(Array.Empty<byte>(), HashMode.MD5)));
        if (length == 0)
          Assert.Equal(sourceInfo.Hash, Hex.ToHex(Hash.ComputeHash(Stream.Null, HashMode.MD5)));
        else
          Assert.NotEqual(sourceInfo.Hash, Hex.ToHex(Hash.ComputeHash(Stream.Null, HashMode.MD5)));
        var targetMd5 = (await client.DownloadObjectAsync(containerName, objectName, Stream.Null).ConfigureAwait(false)).Hash;
        Assert.Equal(sourceInfo.Hash, targetMd5);

        using (var stm = await client.ReadObjectAsync(containerName, objectName).ConfigureAwait(false))
        using (var md5Stream = HashStream.CreateRead(stm, HashMode.MD5)) {
          await md5Stream.CopyToAsync(Stream.Null).ConfigureAwait(false);
          var md5 = md5Stream.ComputeHash();
          Assert.Equal(sourceInfo.Hash, md5);
        }
        var info = await client.GetObjectInfoAsync(containerName, objectName).ConfigureAwait(false);
        Assert.NotNull(info);
        Assert.Equal(length, info.Bytes);
        Assert.Equal(objectName, info.Name);
        Assert.Equal(sourceInfo.Hash, info.Hash);
        Assert.True(Math.Abs((info.LastModified - DateTime.UtcNow).TotalSeconds) < 60);
        Assert.Equal("application/octet-stream", info.ContentType);

        var targetName = Guid.NewGuid().ToString("N");
        await client.CopyObjectAsync(containerName, objectName, containerName, targetName).ConfigureAwait(false);
        info = await client.GetObjectInfoAsync(containerName, targetName).ConfigureAwait(false);
        Assert.NotNull(info);
        Assert.Equal(length, info.Bytes);
        Assert.Equal(targetName, info.Name);
        Assert.Equal(sourceInfo.Hash, info.Hash);
        Assert.True(Math.Abs((info.LastModified - DateTime.UtcNow).TotalSeconds) < 60);
        Assert.Equal("application/octet-stream", info.ContentType);

        await client.DeleteObjectAsync(containerName, objectName).ConfigureAwait(false);
        await client.DeleteObjectAsync(containerName, targetName).ConfigureAwait(false);
        await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
      }
    }

    [Fact]
    public async Task GetUrl() {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");
        var objectName = Guid.NewGuid().ToString("N");
        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        ObjectInfoModel sourceInfo;
        using (var randomStream = new RandomStream(100))
          sourceInfo = await client.UploadObjectAsync(containerName, objectName, new SourceStreamWithUnknownLength(randomStream)).ConfigureAwait(false);

        var url = await client.GetUrlAsync(containerName, objectName);

        await client.DeleteObjectAsync(containerName, objectName).ConfigureAwait(false);
        await client.DeleteContainerAsync(containerName).ConfigureAwait(false);

        var uri = new Uri(url);
        Assert.Equal(5, uri.Segments.Length);
        Assert.Equal(objectName, uri.Segments[4].Trim('/'));
        Assert.Equal(containerName, uri.Segments[3].Trim('/'));
      }
    }

    [Theory]
    [InlineData(100, 20, 20)]
    [InlineData(100, 0, 1)]
    [InlineData(100, 0, 10)]
    [InlineData(100, 0, 100)]
    [InlineData(100, 50, 50)]
    [InlineData(100, 99, 1)]
    public async Task TestRange(int fullLength, int offset, int length) {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");
        var objectName = Guid.NewGuid().ToString("N");
        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        var randomStream = new MemoryStream();
        using (var rndGen = new RandomStream(fullLength))
          await rndGen.CopyToAsync(randomStream).ConfigureAwait(false);
        randomStream.Seek(0, SeekOrigin.Begin);
        var sourceInfo = await client.UploadObjectAsync(containerName, objectName, new IsolatedStream(randomStream), contentType: "application/random").ConfigureAwait(false);
        string rangeMd5;
        randomStream.Seek(offset, SeekOrigin.Begin);
        using (var subStream = new SubStreamRead(randomStream, length))
          rangeMd5 = await Hash.ComputeHashAsync(subStream, HashMode.MD5, CancellationToken.None).ConfigureAwait(false);
        Assert.NotNull(rangeMd5);
        if (offset > 0 || length < fullLength)
          Assert.NotEqual(sourceInfo.Hash, rangeMd5);
        else
          Assert.Equal(sourceInfo.Hash, rangeMd5);

        using (var response = await client.GetAsync(containerName, objectName, r => {
          r.Headers.Range = new RangeHeaderValue(offset, offset + length - 1);
        }))
        using (var md5Stream = HashStream.CreateRead(await response.Content.ReadAsStreamAsync(), HashMode.MD5)) {
          Assert.Equal("application/random", response.Content.Headers.ContentType.ToString());
          Assert.Equal(length, response.Content.Headers.ContentLength);
          await md5Stream.CopyToAsync(Stream.Null).ConfigureAwait(false);
          var md5 = md5Stream.ComputeHash();
          Assert.Equal(rangeMd5, md5);
        }

        await client.DeleteObjectAsync(containerName, objectName).ConfigureAwait(false);
        await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
      }
    }

    [Theory]
    [InlineData(100, 20, 20, 60, 30)]
    public async Task TestRangeMulti(int fullLength, int offset1, int length1, int offset2, int length2) {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");
        var objectName = Guid.NewGuid().ToString("N");
        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        var randomStream = new MemoryStream();
        using (var rndGen = new RandomStream(fullLength))
          await rndGen.CopyToAsync(randomStream).ConfigureAwait(false);
        randomStream.Seek(0, SeekOrigin.Begin);
        var sourceMd5 = await client.UploadObjectAsync(containerName, objectName, new IsolatedStream(randomStream), contentType: "application/random").ConfigureAwait(false);
        string rangeMd51;
        randomStream.Seek(offset1, SeekOrigin.Begin);
        using (var subStream = new SubStreamRead(randomStream, length1))
          rangeMd51 = await Hash.ComputeHashAsync(subStream, HashMode.MD5, CancellationToken.None).ConfigureAwait(false);
        Assert.NotNull(rangeMd51);
        string rangeMd52;
        randomStream.Seek(offset2, SeekOrigin.Begin);
        using (var subStream = new SubStreamRead(randomStream, length2))
          rangeMd52 = await Hash.ComputeHashAsync(subStream, HashMode.MD5, CancellationToken.None).ConfigureAwait(false);
        Assert.NotNull(rangeMd52);

        var range = new RangeHeaderValue() {
          Ranges = {
            new RangeItemHeaderValue(offset1, offset1+length1-1),
            new RangeItemHeaderValue(offset2, offset2+length2-1)
          }
        };
        using (var response = await client.GetAsync(containerName, objectName, r => {
          r.Headers.Range = range;
        })) {
          Assert.Equal("multipart/byteranges", response.Content.Headers.ContentType.MediaType);
          var multipart = await response.Content.ReadAsMultipartAsync();
          Assert.Equal(2, multipart.Contents.Count);

          var stm1 = await multipart.Contents[0].ReadAsStreamAsync();
          Assert.Equal(rangeMd51, Hex.ToHex(Hash.ComputeHash(stm1, HashMode.MD5)));
          var stm2 = await multipart.Contents[1].ReadAsStreamAsync();
          Assert.Equal(rangeMd52, Hex.ToHex(Hash.ComputeHash(stm2, HashMode.MD5)));
        }

        await client.DeleteObjectAsync(containerName, objectName).ConfigureAwait(false);
        await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
      }
    }

    [Fact]
    public async Task TestObjectInfo_NotExists() {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");
        var objectName = Guid.NewGuid().ToString("N");
        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        var info = await client.GetObjectInfoAsync(containerName, objectName).ConfigureAwait(false);
        Assert.Null(info);

        await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
      }
    }

    [Fact]
    public async Task TestDeleteObject_NotExists() {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");
        var objectName = Guid.NewGuid().ToString("N");
        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        var info = await client.GetObjectInfoAsync(containerName, objectName).ConfigureAwait(false);
        Assert.Null(info);

        await Assert.ThrowsAsync<ObjectNotFoundException>(async () => await client.DeleteObjectAsync(containerName, objectName).ConfigureAwait(false)).ConfigureAwait(false);

        await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
      }
    }

    [Fact]
    async Task TempKey_Create_Update() {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");

        await client.CreateContainerAsync(containerName).ConfigureAwait(false);

        var info = await client.GetContainerInfoAsync(containerName).ConfigureAwait(false);

        Assert.Null(info.MetaTempUrlKey);
        Assert.Null(info.MetaTempUrlKey2);
        Assert.Null(info.MetaTempUrlKeyCreated);
        Assert.Null(info.MetaTempUrlKey2Created);

        var now = DateTime.Now;
        now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

        await client.SetContainerInfoAsync(containerName, new ContainerDetailsModel() {
          MetaTempUrlKey = "abc",
          MetaTempUrlKeyCreated = now
        }).ConfigureAwait(false);

        info = await client.GetContainerInfoAsync(containerName).ConfigureAwait(false);

        Assert.NotNull(info.MetaTempUrlKey);
        Assert.Null(info.MetaTempUrlKey2);
        Assert.NotNull(info.MetaTempUrlKeyCreated);
        Assert.Null(info.MetaTempUrlKey2Created);
        Assert.Equal("abc", info.MetaTempUrlKey);
        Assert.Equal(now, info.MetaTempUrlKeyCreated);
      }
    }

    [Fact]
    public async Task TempUrl_Get() {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");

        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        var tempUrlService = provider.GetRequiredService<ISwiftTempUrlService>();

        var objectName = Guid.NewGuid().ToString("N");
        ObjectInfoModel sourceInfo;
        using (var randomStream = new RandomStream(10000))
          sourceInfo = await client.UploadObjectAsync(containerName, objectName, randomStream).ConfigureAwait(false);
        Assert.NotNull(sourceInfo);
        Assert.NotNull(sourceInfo.Hash);
        var publicLink = await tempUrlService.CreateGetTempUrlAsync(containerName, objectName).ConfigureAwait(false);
        using (var c = new HttpClient())
        using (var s = await c.GetStreamAsync(publicLink).ConfigureAwait(false))
        using (var md5Stream = HashStream.CreateWrite(Stream.Null, HashMode.MD5)) {
          await s.CopyToAsync(md5Stream).ConfigureAwait(false);
          var targetMd5 = md5Stream.ComputeHash();
          Assert.Equal(sourceInfo.Hash, targetMd5);
        }

        var publicLink2 = await tempUrlService.CreateGetTempUrlAsync(containerName, objectName).ConfigureAwait(false);

        await client.DeleteObjectAsync(containerName, objectName).ConfigureAwait(false);
        await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
      }
    }

    [Fact]
    public async Task TempUrl_Put_Get() {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");

        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        var tempUrlService = provider.GetRequiredService<ISwiftTempUrlService>();

        var objectName = Guid.NewGuid().ToString("N");
        string sourceMd5;
        var putUrl = await tempUrlService.CreatePutTempUrlAsync(containerName, objectName).ConfigureAwait(false);
        using (var randomStream = new RandomStream(10000))
        using (var hashStream = HashStream.CreateRead(randomStream, HashMode.MD5)) {
          var buffer = new byte[randomStream.Length];
          await hashStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
          using var httpClient = new HttpClient();
          await httpClient.PutAsync(putUrl, new ByteArrayContent(buffer));
          sourceMd5 = hashStream.ComputeHash();
        }
        Assert.NotNull(sourceMd5);
        await Task.Delay(2000).ConfigureAwait(false);
        var publicLink = await tempUrlService.CreateGetTempUrlAsync(containerName, objectName).ConfigureAwait(false);
        using (var c = new HttpClient())
        using (var s = await c.GetStreamAsync(publicLink).ConfigureAwait(false))
        using (var md5Stream = HashStream.CreateWrite(Stream.Null, HashMode.MD5)) {
          await s.CopyToAsync(md5Stream).ConfigureAwait(false);
          var targetMd5 = md5Stream.ComputeHash();
          Assert.Equal(sourceMd5, targetMd5);
        }

        var publicLink2 = await tempUrlService.CreateGetTempUrlAsync(containerName, objectName).ConfigureAwait(false);

        await client.DeleteObjectAsync(containerName, objectName).ConfigureAwait(false);
        await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
      }
    }

    [Fact]
    public async Task TempUrl_Get_WithDisposition() {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");

        await client.CreateContainerAsync(containerName).ConfigureAwait(false);
        var tempUrlService = provider.GetRequiredService<ISwiftTempUrlService>();

        var objectName = Guid.NewGuid().ToString("N");
        ObjectInfoModel sourceInfo;
        using (var randomStream = new RandomStream(10000))
          sourceInfo = await client.UploadObjectAsync(containerName, objectName, randomStream).ConfigureAwait(false);
        Assert.NotNull(sourceInfo);
        Assert.NotNull(sourceInfo.Hash);
        var publicLink = await tempUrlService.CreateGetTempUrlAsync(containerName, objectName, "test.dat", true).ConfigureAwait(false);
        using (var c = new HttpClient())
        using (var r = await c.GetAsync(publicLink).ConfigureAwait(false))
        using (var md5Stream = HashStream.CreateWrite(Stream.Null, HashMode.MD5)) {
          Assert.Equal("test.dat", r.Content.Headers.ContentDisposition.FileName.Trim('\"'));
          Assert.Equal("inline", r.Content.Headers.ContentDisposition.DispositionType);
          await r.Content.CopyToAsync(md5Stream).ConfigureAwait(false);
          var targetMd5 = md5Stream.ComputeHash();
          Assert.Equal(sourceInfo.Hash, targetMd5);
        }

        await client.DeleteObjectAsync(containerName, objectName).ConfigureAwait(false);
        await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
      }
    }

    [Theory]
    [InlineData(10, 4)]
    [InlineData(10, 10)]
    [InlineData(10, 100)]
    public async Task TestBrowseObjects(int objectsCount, int pageSize) {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");

        await client.CreateContainerAsync(containerName).ConfigureAwait(false);

        var prefix = "folder1/folder2/";
        var objectsNames = Enumerable.Repeat(Guid.Empty, objectsCount).Select(x => Guid.NewGuid().ToString("N")).ToArray();
        for (var i = 0; i < objectsCount; i++) {
          var name = prefix + objectsNames[i];
          await client.UploadObjectAsync(containerName, name, new MemoryStream(new byte[1])).ConfigureAwait(false);
        }

        await Task.Delay(2000).ConfigureAwait(false);
        try {
          string marker = null;
          var browsedObjects = new List<ObjectInfoModel>();
          do {
            var objects = await client.BrowseObjectsAsync(containerName, prefix, marker, pageSize).ConfigureAwait(false);
            if (objects.Length == 0)
              break;
            browsedObjects.AddRange(objects);
            marker = browsedObjects.Last().Name;
          } while (true);
          Assert.Equal(objectsCount, browsedObjects.Count);
          Assert.Equal(objectsNames.Select(x => prefix + x).OrderBy(x => x), browsedObjects.Select(x => x.Name));
        }
        finally {
          for (var i = 0; i < objectsCount; i++) {
            var name = prefix + objectsNames[i];
            await client.DeleteObjectAsync(containerName, name).ConfigureAwait(false);
          }
          await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
        }
      }
    }

    [Theory]
    [InlineData(10, 4)]
    [InlineData(10, 10)]
    [InlineData(10, 100)]
    public async Task TestBrowseContainers(int containersCount, int pageSize) {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containers = new List<string>();
        for (var i = 0; i < containersCount; i++) {
          var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");
          await client.CreateContainerAsync(containerName).ConfigureAwait(false);
          containers.Add(containerName);
        }

        try {
          string marker = null;
          var browsedContainers = new List<ContainerInfoModel>();
          do {
            var objects = await client.BrowseContainersAsync(marker, pageSize).ConfigureAwait(false);
            if (objects.Length == 0)
              break;
            browsedContainers.AddRange(objects);
            marker = browsedContainers.Last().Name;
          } while (true);
          Assert.True(browsedContainers.Count >= containersCount);
        }
        finally {
          foreach (var containerName in containers)
            await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
        }
      }
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(10, 4)]
    [InlineData(10, 10)]
    [InlineData(10, 100)]
    public async Task TestEnumerate(int objectsCount, int pageSize) {
      using (var provider = CreateProvider()) {
        var client = provider.GetRequiredService<ISwiftClient>();
        var containerName = TestContainerPrefix + Guid.NewGuid().ToString("N");

        await client.CreateContainerAsync(containerName).ConfigureAwait(false);

        var prefix = "folder1/folder2/";
        var objectsNames = Enumerable.Repeat(Guid.Empty, objectsCount).Select(x => Guid.NewGuid().ToString("N")).ToArray();
        for (var i = 0; i < objectsCount; i++) {
          var name = prefix + objectsNames[i];
          await client.UploadObjectAsync(containerName, name, new MemoryStream(new byte[1])).ConfigureAwait(false);
        }

        try {
          var browsedObjects = new List<ObjectInfoModel>();
          await foreach (var obj in client.EnumerateObjectsAsync(containerName, prefix, pageSize)) {
            Assert.NotNull(obj);
            Assert.NotNull(obj.Name);
            Assert.NotEmpty(obj.Name);
            Assert.NotNull(obj.Hash);
            Assert.Equal(32, obj.Hash.Length);
            Assert.True(Math.Abs((obj.LastModified - DateTime.UtcNow).TotalSeconds) < 60);
            Assert.NotNull(obj.ContentType);
            Assert.True(obj.Bytes > 0);
            browsedObjects.Add(obj);
          }

          Assert.Equal(objectsCount, browsedObjects.Count);
          Assert.Equal(objectsNames.Select(x => prefix + x).OrderBy(x => x), browsedObjects.Select(x => x.Name));
        }
        finally {
          for (var i = 0; i < objectsCount; i++) {
            var name = prefix + objectsNames[i];
            await client.DeleteObjectAsync(containerName, name).ConfigureAwait(false);
          }
          await client.DeleteContainerAsync(containerName).ConfigureAwait(false);
        }
      }
    }
  }
}