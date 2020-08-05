using System;
using System.IO;
using ClickHouse.Ado.Impl.Data;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace ClickHouse.Ado.Impl.Compress {
    internal class Lz4Compressor : HashingCompressor {
        private static readonly byte[] Header = {0x82};

        private readonly bool _useHc;

        public Lz4Compressor(bool useHc, ClickHouseConnectionSettings settings) : base(settings) => _useHc = useHc;

        public override CompressionMethod Method => _useHc ? CompressionMethod.Lz4Hc : CompressionMethod.Lz4;

        private LZ4Level Level => _useHc ? LZ4Level.L09_HC : LZ4Level.L00_FAST;

        protected override byte[] Compress(MemoryStream uncompressed) {
            using (var output = new MemoryStream())
            using (var encoder = LZ4Stream.Encode(output, Level))
            {
                uncompressed.CopyTo(encoder);
                return output.ToArray();
            }
        }

        protected override byte[] Decompress(Stream compressed, out UInt128 compressedHash) {
            var header = new byte[9];
            var read = 0;
            do {
                read += compressed.Read(header, read, header.Length - read);
            } while (read < header.Length);

            if (header[0] != Header[0])
                throw new FormatException($"Invalid header value {header[0]}");

            var compressedSize = BitConverter.ToInt32(header, 1);
            var uncompressedSize = BitConverter.ToInt32(header, 5);
            read = 0;
            compressedSize -= header.Length;
            var compressedBytes = new byte[compressedSize + header.Length];
            Array.Copy(header, 0, compressedBytes, 0, header.Length);
            do {
                read += compressed.Read(compressedBytes, header.Length + read, compressedSize - read);
            } while (read < compressedSize);

            compressedHash = ClickHouseCityHash.CityHash128(compressedBytes);
            //return LZ4Codec.Decode(compressedBytes, header.Length, compressedSize, uncompressedSize);
            using (var output = new MemoryStream())
            using (var encoder = LZ4Stream.Decode(output))
            {
                compressed.CopyTo(encoder);
                return output.ToArray();
            }

        }
    }
}
