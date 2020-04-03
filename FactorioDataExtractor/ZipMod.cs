using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FactorioDataExtractor
{
    internal class ZipMod : Mod
    {
        private readonly FileStream stream;
        private readonly ZipArchive zipArchive;
        private readonly string directoryName;

        public ZipMod(string fileName)
        {
            stream = File.OpenRead(fileName);
            zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
            directoryName = zipArchive.Entries.First().FullName;
            directoryName = directoryName.Remove(directoryName.IndexOf('/'));
        }

        public override bool FileExists(string fileName)
        {
            var entry = GetEntry(fileName);
            return entry != null;
        }

        public override string LoadText(string fileName)
        {
            var entry = GetEntry(fileName);
            Debug.Assert(entry != null, nameof(entry) + " != null");
            using var entryStream = entry.Open();
            return new StreamReader(entryStream).ReadToEnd();
        }

        public override string TryLoadText(string fileName)
        {
            var entry = GetEntry(fileName);
            if (entry == null)
            {
                return null;
            }

            using var entryStream = entry.Open();
            return new StreamReader(entryStream).ReadToEnd();
        }

        private ZipArchiveEntry GetEntry(string fileName)
        {
            return zipArchive.GetEntry(directoryName + "/" + fileName.Replace('\\', '/').Replace("//", "/"));
        }
    }
}