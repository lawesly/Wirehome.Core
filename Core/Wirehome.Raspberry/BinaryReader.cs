﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Wirehome.Contracts.Core;

namespace Wirehome.Raspberry
{
    public class BinaryReader : IBinaryReader
    {
        private DataReader _dataReader;

        public BinaryReader(IInputStream stream)
        {
            _dataReader = new DataReader(stream)
            {
                ByteOrder = ByteOrder.LittleEndian,
                InputStreamOptions = InputStreamOptions.Partial
            };
        }

        public void Dispose()
        {
            _dataReader.DetachStream();
            _dataReader.Dispose();
        }

        public byte ReadByte() => _dataReader.ReadByte();
        public float ReadSingle() => _dataReader.ReadSingle();
        public string ReadString(byte size) =>_dataReader.ReadString(size);
        public uint ReadUInt32() => _dataReader.ReadUInt32();
        public Task<uint> LoadAsync(uint count, CancellationToken cancellationToken) => _dataReader.LoadAsync(count).AsTask(cancellationToken);
        
    }
}
