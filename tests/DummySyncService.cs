﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using SAPTeam.AndroCtrl.Adb.Interfaces;

namespace SAPTeam.AndroCtrl.Adb.Tests
{
    internal class DummySyncService : ISyncService
    {
        public Dictionary<string, Stream> UploadedFiles
        { get; private set; } = new Dictionary<string, Stream>();

        public bool IsOpen => true;

        public void Dispose()
        {
        }

        public IEnumerable<FileStatistics> GetDirectoryListing(string remotePath)
        {
            throw new NotImplementedException();
        }

        public void Open()
        {
        }

        public void Pull(string remotePath, Stream stream, IProgress<int> progress, CancellationToken cancellationToken)
        {
        }

        public void Push(Stream stream, string remotePath, int permissions, DateTimeOffset timestamp, IProgress<int> progress, CancellationToken cancellationToken)
        {
            UploadedFiles.Add(remotePath, stream);
        }

        public FileStatistics Stat(string remotePath)
        {
            throw new NotImplementedException();
        }
    }
}