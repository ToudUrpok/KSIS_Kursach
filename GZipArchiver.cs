using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using System.IO;

namespace ProxyServer
{
    public class GZipArchiver
    {
        public byte[] Compress(byte[] dataToCompress)
        {
            using (var compressedDataStream = new MemoryStream())
            {
                using (var dataToCompressStream = new MemoryStream(dataToCompress))
                {
                    using (var compressionStream =
                        new GZipStream(compressedDataStream, CompressionMode.Compress))
                    {
                        dataToCompressStream.CopyTo(compressionStream);
                        return compressedDataStream.ToArray();
                    }
                }
            }
        }
        
        public byte[] Decompress(byte[] compressedData)
        {
            using (var decompressedDataStream = new MemoryStream())
            {
                using (var compressedDataStream = new MemoryStream(compressedData))
                {
                    using (var decompressionStream = 
                        new GZipStream(compressedDataStream, CompressionMode.Decompress))
                    {
                        decompressionStream.CopyTo(decompressedDataStream);
                        return decompressedDataStream.ToArray();
                    }
                }
            }
        }
    }
}
