using Aliyun.OSS;
using Aliyun.OSS.Common;
using FubarDev.FtpServer.BackgroundTransfer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FubarDev.FtpServer.FileSystem.Oss
{
    public class OssFileSystem : IUnixFileSystem
    {
        private readonly OssFileSystemOptions _options;
        private OssClient _client;

        public OssFileSystem(OssFileSystemOptions options, string rootDirectory)
        {
            _options = options;
            _client = new OssClient(options.Endpoint, options.AccessKeyId, options.AccessKeySecret);
            Root = new OssDirectoryEntry(rootDirectory, true);
        }

        public bool SupportsAppend => false;

        public bool SupportsNonEmptyDirectoryDelete => true;

        public StringComparer FileSystemEntryComparer => StringComparer.Ordinal;

        public IUnixDirectoryEntry Root { get; }

        public Task<IBackgroundTransfer> AppendAsync(IUnixFileEntry fileEntry, long? startPosition, Stream data, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// 创建文件
        /// </summary>
        /// <param name="targetDirectory"></param>
        /// <param name="fileName"></param>
        /// <param name="data"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IBackgroundTransfer> CreateAsync(IUnixDirectoryEntry targetDirectory, string fileName, Stream data, CancellationToken cancellationToken)
        {
            var key = OssPath.Combine(((OssDirectoryEntry)targetDirectory).Key, fileName);
            await UploadFile(data, key, cancellationToken);
            return default;
        }

        /// <summary>
        /// 创建路径
        /// </summary>
        /// <param name="targetDirectory"></param>
        /// <param name="directoryName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<IUnixDirectoryEntry> CreateDirectoryAsync(IUnixDirectoryEntry targetDirectory, string directoryName, CancellationToken cancellationToken)
        {
            var key = OssPath.Combine(((OssDirectoryEntry)targetDirectory).Key, directoryName + "/");

            try
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    await Task.Run(() =>
                    {
                        _client.PutObject(_options.BucketName, key, memStream);
                    });
                    
                }
            }
            catch (OssException ex)
            {
                throw ex;
            }

            return new OssDirectoryEntry(key);
        }

        public async Task<IReadOnlyList<IUnixFileSystemEntry>> GetEntriesAsync(IUnixDirectoryEntry directoryEntry, CancellationToken cancellationToken)
        {
            var prefix = ((OssDirectoryEntry)directoryEntry).Key;

            if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith("/"))
            {
                prefix += '/';
            }

            return await ListObjectsAsync(prefix, false, cancellationToken);
        }

        public async Task<IUnixFileSystemEntry> GetEntryByNameAsync(IUnixDirectoryEntry directoryEntry, string name, CancellationToken cancellationToken)
        {
            var key = OssPath.Combine(((OssDirectoryEntry)directoryEntry).Key, name);

            var entry = await GetObjectAsync(key, cancellationToken);

            if (entry != null)
            {
                return entry;
            }

            // not a file search for directory
            key += '/';
            var objects = await ListObjectsAsync(key, true, cancellationToken);

            if (objects.Count > 0)
            {
                return new OssDirectoryEntry(key);
            }

            return null;
        }

        public async Task<IUnixFileSystemEntry> MoveAsync(IUnixDirectoryEntry parent, IUnixFileSystemEntry source, IUnixDirectoryEntry target, string fileName, CancellationToken cancellationToken)
        {
            var sourceKey = ((OssFileSystemEntry)source).Key;
            var key = OssPath.Combine(((OssDirectoryEntry)target).Key, fileName);

            if (source is OssFileEntry file)
            {
                await MoveFile(sourceKey, key);
                return new OssFileEntry(key, file.Size)
                {
                    LastWriteTime = file.LastWriteTime ?? DateTimeOffset.UtcNow,
                };
            }

            if (source is OssDirectoryEntry)
            {
                key += '/';
                ObjectListing response;
                do
                {
                    response = _client.ListObjects(_options.BucketName, sourceKey);
                    foreach (var s3Object in response.ObjectSummaries)
                    {
                        await MoveFile(s3Object.Key, key + s3Object.Key.Substring(sourceKey.Length));
                    }
                }
                while (response.IsTruncated);

                return new OssDirectoryEntry(key);
            }

            throw new InvalidOperationException();
        }

        public async Task<Stream> OpenReadAsync(IUnixFileEntry fileEntry, long startPosition, CancellationToken cancellationToken)
        {
            var stream = await Task.Run(async () =>
            {
                return _client.GetObject(_options.BucketName, ((OssFileSystemEntry)fileEntry).Key).Content;
            });

            if (startPosition != 0)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
            }

            return stream;
        }

        public async Task<IBackgroundTransfer> ReplaceAsync(IUnixFileEntry fileEntry, Stream data, CancellationToken cancellationToken)
        {
            await UploadFile(data, ((OssFileEntry)fileEntry).Key, cancellationToken);
            return default;
        }

        public Task<IUnixFileSystemEntry> SetMacTimeAsync(IUnixFileSystemEntry entry, DateTimeOffset? modify, DateTimeOffset? access, DateTimeOffset? create, CancellationToken cancellationToken)
        {
            return Task.FromResult(entry);
        }

        public async Task UnlinkAsync(IUnixFileSystemEntry entry, CancellationToken cancellationToken)
        {
            await Task.Factory.StartNew(() => { 
                return _client.DeleteObject(_options.BucketName, ((OssFileSystemEntry)entry).Key);
            }); 
        }

        private async Task<IUnixFileSystemEntry> GetObjectAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                var exist = await Task.Run(() =>
                {
                    return _client.DoesObjectExist(_options.BucketName, key);
                });

                if (!exist)
                {
                    return null;
                }

                var result = await Task.Run(() =>
                {
                    return _client.GetObject(_options.BucketName, key);
                });

                if (key.EndsWith("/"))
                {
                    return new OssDirectoryEntry(key);
                }
                return new OssFileEntry(key, result.Metadata.ContentLength)
                {
                    LastWriteTime = result.Metadata.LastModified,
                };
            }
            catch (OssException ex)
            {
                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 获取文件列表
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="includeSelf"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<IReadOnlyList<IUnixFileSystemEntry>> ListObjectsAsync(string prefix, bool includeSelf, CancellationToken cancellationToken)
        {
            var objects = new List<IUnixFileSystemEntry>();
            ObjectListing result = null;
            string nextMarker = string.Empty;

            do
            {
                var listObjectsRequest = new ListObjectsRequest(_options.BucketName)
                {
                    Marker = nextMarker,
                    Prefix = prefix,
                    Delimiter = "/",
                };

                result = await Task.Run(() => {
                    return _client.ListObjects(listObjectsRequest);
                });

                foreach (var directory in result.CommonPrefixes)
                {
                    objects.Add(new OssDirectoryEntry(directory));
                }

                foreach (var s3Object in result.ObjectSummaries)
                {
                    if (s3Object.Key.EndsWith("/") && s3Object.Key == prefix)
                    {
                        if (includeSelf)
                        {
                            objects.Add(new OssDirectoryEntry(s3Object.Key));
                        }

                        continue;
                    }

                    objects.Add(
                        new OssFileEntry(s3Object.Key, s3Object.Size)
                        {
                            LastWriteTime = s3Object.LastModified,
                        });
                }

                nextMarker = result.NextMarker;
            }
            while (result.IsTruncated);

            return objects;
        }

        /// <summary>
        /// 移动文件
        /// </summary>
        /// <param name="sourceKey"></param>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task MoveFile(string sourceKey, string key)
        {
            try
            {
                await Task.Run(() =>
                {
                    var req = new CopyObjectRequest(_options.BucketName, sourceKey, _options.BucketName, key);
                    _client.CopyObject(req);
                    _client.DeleteObject(_options.BucketName, sourceKey);
                });
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task UploadFile(Stream data, string key, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Run(() =>
                {
                    _client.PutObject(_options.BucketName, key, data);
                });
            }
            catch (OssException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                data.Close();
                data.Dispose();
                GC.Collect();
            }
        }
    }
}
