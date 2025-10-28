using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FTPApp
{
    internal class FileHelper
    {

        private static string Sanitize(string input)
        {
            // make sure email doesn’t break file name
            foreach (var c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');
            return input.Replace("@", "_at_");
        }
        public static bool IsZipFile(string path)
        {
            return Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase);
        }

        public static string CreateZipFromFolder(string folderToZip, string userEmail)
        {
            var cache = FileSystem.CacheDirectory;
            var zipFileName = Path.Combine(cache, $"{userEmail}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
            if (File.Exists(zipFileName)) File.Delete(zipFileName);
            ZipFile.CreateFromDirectory(folderToZip, zipFileName, CompressionLevel.Optimal, includeBaseDirectory: false);
            return zipFileName;
        }

        public static string CreateZipFromFile(string filePath, string userEmail)
        {
            var cache = FileSystem.CacheDirectory;
            var zipFileName = Path.Combine(cache, $"{userEmail}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
            if (File.Exists(zipFileName)) File.Delete(zipFileName);
            using var zip = ZipFile.Open(zipFileName, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
            return zipFileName;
        }
    }
}

