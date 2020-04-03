using System.IO;

namespace FactorioDataExtractor
{
    internal class DirectoryMod : Mod
    {
        private readonly DirectoryInfo directory;

        public DirectoryMod(string fileName)
        {
            directory = new DirectoryInfo(fileName);
        }

        public override bool FileExists(string fileName)
        {
            return File.Exists(Path.Combine(directory.FullName, fileName));
        }

        public override string LoadText(string fileName)
        {
            return File.ReadAllText(Path.Combine(directory.FullName, fileName));
        }

        public override string TryLoadText(string fileName)
        {
            try
            {
                return File.ReadAllText(Path.Combine(directory.FullName, fileName));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}