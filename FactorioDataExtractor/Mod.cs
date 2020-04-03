using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FactorioDataExtractor
{
    internal abstract class Mod
    {
        private ModInfo modInfo;

        public override string ToString()
        {
            return Title;
        }

        public static Mod Create(string fileName)
        {
            Mod mod;
            if (Directory.Exists(fileName))
            {
                mod = new DirectoryMod(fileName);
            }
            else if (File.Exists(fileName))
            {
                mod = new ZipMod(fileName);
            }
            else
            {
                throw new NotImplementedException();
            }

            mod.Initialise();
            return mod;
        }

        public abstract bool FileExists(string fileName);
        public abstract string LoadText(string fileName);
        public abstract string TryLoadText(string fileName);

        private void Initialise()
        {
            var text = LoadText("info.json");
            modInfo = JsonSerializer.Deserialize<ModInfo>(text);
            Dependancies = modInfo.Dependencies?.Select(ParseDependancy).ToList() ?? new List<Dependency>();
        }

        private static Dependency ParseDependancy(string value)
        {
            var result = Regex.Match(value, @"^\s*(?<prefix>!|\?|\(\?\))?\s*(?<name>.+?)\s*((?<equality><|<=|=|>=|>)\s*(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)\s*)?$");
            if (!result.Success)
            {
                throw new NotImplementedException();
            }

            var prefix = ParsePrefix(result.Groups["prefix"].Value);
            var name = result.Groups["name"].Value;
            var equality = ParseEquality(result.Groups["equality"].Value);
            Version version;
            if (equality != DependencyEquality.None)
            {
                version = new Version(
                    int.Parse(result.Groups["major"].Value),
                    int.Parse(result.Groups["minor"].Value),
                    int.Parse(result.Groups["patch"].Value));
            }
            else
            {
                version = default;
            }

            return new Dependency
            {
                Type = prefix,
                Name = name,
                Equality = equality,
                Version = version,
            };
        }

        private static DependencyType ParsePrefix(string value)
        {
            switch (value)
            {
                case "":
                    return DependencyType.Hard;
                case "!":
                    return DependencyType.Incompatible;
                case "?":
                    return DependencyType.Optional;
                case "(?)":
                    return DependencyType.HiddenOptional;
                default:
                    throw new NotImplementedException();
            }
        }

        private static DependencyEquality ParseEquality(string value)
        {
            switch (value)
            {
                case "":
                    return DependencyEquality.None;
                case "<":
                    return DependencyEquality.LessThan;
                case "<=":
                    return DependencyEquality.LessEquals;
                case "=":
                    return DependencyEquality.Equals;
                case ">=":
                    return DependencyEquality.GreaterEquals;
                case ">":
                    return DependencyEquality.GreaterThan;
                default:
                    throw new NotImplementedException();
            }
        }

        public string Title => modInfo.Title;
        public string InternalName => modInfo.Name;
        public IList<Dependency> Dependancies { get; internal set; }
        public Version Version => new Version(modInfo.Version ?? "0.0.0");
    }
}