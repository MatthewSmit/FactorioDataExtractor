using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NLua;
using NLua.Exceptions;
using Lua = NLua.Lua;
using LuaFunction = NLua.LuaFunction;

namespace FactorioDataExtractor
{
    internal static class Program
    {
        private static Mod currentMod;
        private static string currentFile;

        private static void Main(string[] args)
        {
            var factorioDirectory = args[0];
            var factorioModDirectory = args.Length > 1
                ? args[1]
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Factorio",
                    "mods");

            var modList = CreateModList(factorioDirectory, factorioModDirectory);
            SortModList(modList);
            modList.Insert(0, Mod.Create(Path.Combine(factorioDirectory, "data/core")));

            IList<ModSetting> settings;
            using (var lua = CreateLuaContext(modList))
            {
                lua.DoString(@"
data['bool-setting'] = {}
data['int-setting'] = {}
data['double-setting'] = {}
data['string-setting'] = {}
", "<startup2>");
                foreach (var mod in modList)
                {
                    RunFile(lua, mod, "settings.lua");
                }
                foreach (var mod in modList)
                {
                    RunFile(lua, mod, "settings-updates.lua");
                }
                foreach (var mod in modList)
                {
                    RunFile(lua, mod, "settings-final-fixes.lua");
                }

                var data = lua.GetTable("data.raw");
                settings = ParseSettings(data);
            }

            using (var lua = CreateLuaContext(modList))
            {
                lua.NewTable("settings");
                lua.NewTable("settings.startup");
                foreach (var setting in settings)
                {
                    if (setting.SettingType == "startup")
                    {
                        AddSetting(lua, setting);
                    }
                }
                foreach (var mod in modList)
                {
                    RunFile(lua, mod, "data.lua");
                }
                foreach (var mod in modList)
                {
                    RunFile(lua, mod, "data-updates.lua");
                }
                foreach (var mod in modList)
                {
                    RunFile(lua, mod, "data-final-fixes.lua");
                }

                var data = lua.GetTable("data.raw");
                var json = JsonSerializer.Serialize(new
                {
                    Settings = settings,
                    Data = data,
                }, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters =
                    {
                        new DoubleConverter(),
                        new TableConverter(),
                    }
                });
                File.WriteAllText("dump.json", json);
            }
        }

        private static void AddSetting(Lua lua, ModSetting setting)
        {
            var path = "settings.startup." + setting.Name;
            lua.NewTable(path);
            var table = lua.GetTable(path);
            table["value"] = setting.DefaultValue;
        }

        private static IList<ModSetting> ParseSettings(LuaTable data)
        {
            var list = new List<ModSetting>();
            foreach (KeyValuePair<object, object> setting in (LuaTable)data["bool-setting"])
            {
                list.Add(ParseSettingValue((LuaTable)setting.Value));
            }
            foreach (KeyValuePair<object, object> setting in (LuaTable)data["int-setting"])
            {
                list.Add(ParseSettingValue((LuaTable)setting.Value));
            }
            foreach (KeyValuePair<object, object> setting in (LuaTable)data["double-setting"])
            {
                list.Add(ParseSettingValue((LuaTable)setting.Value));
            }
            foreach (KeyValuePair<object, object> setting in (LuaTable)data["string-setting"])
            {
                list.Add(ParseSettingValue((LuaTable)setting.Value));
            }
            return list;
        }

        private static ModSetting ParseSettingValue(LuaTable value)
        {
            return new ModSetting
            {
                Type = (string)value["type"],
                Name = (string)value["name"],
                SettingType = (string)value["setting_type"],
                DefaultValue = value["default_value"],
                // LocalisedName = value["localised_name"],
                // LocalisedDescription = value["localised_description"],
                MaximumValue = value["maximum_value"],
                MinimumValue = value["minimum_value"],
                // AllowedValues = value["allowed_values"],
                AllowBlank = value["allow_blank"],
                Order = (string)value["order"],
            };
        }

        private static Lua CreateLuaContext(IList<Mod> modList)
        {
            var lua = new Lua();
            lua.State.Encoding = Encoding.UTF8;
            lua.DoString(File.ReadAllText("pprint.lua"), "<ppriunt>");
            lua.DoString(File.ReadAllText("serpent.lua"), "<serpent>");

            lua.DoString(@"
defines = {}

defines.difficulty_settings = {}
defines.difficulty_settings.recipe_difficulty = {
    normal = 'normal',
}
defines.difficulty_settings.technology_difficulty = {
    normal = 'normal',
}

defines.direction = {
    north = 'north',
    east = 'east',
    south = 'south',
    west = 'west',
}

defines.entity_status = {}
defines.entity_status.working = nil
defines.entity_status.no_power = nil
defines.entity_status.no_fuel = nil
defines.entity_status.no_recipe = nil
defines.entity_status.no_input_fluid = nil
defines.entity_status.no_research_in_progress = nil
defines.entity_status.no_minable_resources = nil
defines.entity_status.low_input_fluid = nil
defines.entity_status.low_power = nil
defines.entity_status.disabled_by_control_behavior = nil
defines.entity_status.disabled_by_script = nil
defines.entity_status.fluid_ingredient_shortage = nil
defines.entity_status.fluid_production_overload = nil
defines.entity_status.item_ingredient_shortage = nil
defines.entity_status.item_production_overload = nil
defines.entity_status.marked_for_deconstruction = nil
defines.entity_status.missing_required_fluid = nil
defines.entity_status.missing_science_packs = nil
defines.entity_status.waiting_for_source_items = nil
defines.entity_status.waiting_for_space_in_destination = nil
defines.entity_status.waiting_to_launch_rocket = nil
", "<defines>");

            lua.DoString(@"
data = {}
data.raw = {}
function merge(t1, t2)
    if t1 == nil then return t2 end
    for k, v in pairs(t2) do
        if (type(v) == 'table') and (type(t1[k] or false) == 'table') then
            merge(t1[k], t2[k])
        else
            t1[k] = v
        end
    end
    return t1
end
-- LINE 15
function data:extend(t)
    -- print('############')
    -- pprint(t)
    for k, v in pairs(t) do
        -- print('-----------------')
        -- pprint(k)
        -- pprint(v)
        if type(v) == 'table' and v.type ~= nil then
            if self.raw[v.type] == nil then
                self.raw[v.type] = {}
            end
            self.raw[v.type][v.name] = merge(self.raw[v.type][v.name], v)
        end
    end
end
function table_size(t)
    local count = 0
    for k, v in pairs(t) do
        count = count + 1
    end
    return count
end
", "<startup>");
            lua.NewTable("mods");
            var modTable = lua.GetTable("mods");
            foreach (var mod in modList)
            {
                modTable[mod.InternalName] = mod.Version.ToString(3);
            }

            string TranslateName(string name)
            {
                var path = (string)lua.DoString(@"function script_path()
                    local str = debug.getinfo(3, 'S')
                    return str.source
                end

                return script_path()")[0];

                if (path.EndsWith(".lua"))
                {
                    var lastIndex = path.LastIndexOfAny(new[] { '/', '\\' });
                    path = path.Remove(lastIndex);
                }
                else
                {
                    path = null;
                }

                name = name.Replace('.', '/');
                if (!name.EndsWith(".lua"))
                {
                    name += ".lua";
                }

                if (currentMod.FileExists(name))
                {
                    return "__" + currentMod.InternalName + "__/" + name;
                }

                var modName = name.Split('/')[0];
                if (Regex.IsMatch(modName, "^__.+__$"))
                {
                    modName = modName[2..^2];
                    var mod = modList.FirstOrDefault(mod => mod.InternalName == modName);
                    if (mod != null)
                    {
                        return name;
                    }
                }

                if (path != null)
                {
                    modName = path.Split('/')[0];
                    if (Regex.IsMatch(modName, "^__.+__$"))
                    {
                        modName = modName[2..^2];
                        var mod = modList.FirstOrDefault(mod => mod.InternalName == modName);
                        if (mod != null)
                        {
                            var relativeName = path.Contains("/") 
                                ?  Path.Combine(path.Substring(modName.Length + 5), name)
                                : name;
                            if (mod.FileExists(relativeName))
                            {
                                return "__" + mod.InternalName + "__/" + relativeName;
                            }
                        }
                    }
                }

                var coreMod = modList.First(mod => mod.InternalName == "core");
                {
                    var relativeName = Path.Combine("lualib", name);
                    if (coreMod.FileExists(relativeName))
                    {
                        return Path.Combine("__core__", relativeName).Replace('\\', '/');
                    }
                }

                throw new NotImplementedException();
            }

            lua["require"] = (Func<string, object>)(name =>
            {
                var fullName = TranslateName(name);
                var luaName = fullName.Replace('.', '/');
                var loaded= lua.GetTable("package.loaded");
                if (loaded[luaName] != null)
                {
                    return loaded[luaName];
                }

                var modName = fullName.Split('/')[0];
                modName = modName[2..^2];
                var mod = modList.FirstOrDefault(mod => mod.InternalName == modName);
                var code = mod.LoadText(fullName.Substring(modName.Length + 5));
                var result = lua.DoString(code, fullName);
                if (result != null)
                {
                    loaded[luaName] = result[0];
                    return result[0];
                }

                loaded[luaName] = true;
                return null;
            });

            var math = lua.GetTable("math");
            math["pow"] = (Func<double, double, double>)Math.Pow;

            lua["log"] = (Action<object>)Console.WriteLine;

            return lua;
        }

        private static void RunFile(Lua lua, Mod mod, string luaFile)
        {
            currentMod = mod;
            var code = mod.TryLoadText(luaFile);
            if (code != null)
            {
                lua.DoString(code, "__" + mod.InternalName + "__/" + luaFile);
            }
        }

        private static IList<Mod> CreateModList(string factorioDirectory, string factorioModDirectory)
        {
            var realModList = new List<Mod>();
            if (File.Exists(Path.Combine(factorioModDirectory, "mod-list.json")))
            {
                var modList = JsonSerializer.Deserialize<FactorioModList>(File.ReadAllText(Path.Combine(factorioModDirectory, "mod-list.json")));

                Mod GetModFromInfo(FactorioModList.Info info)
                {
                    if (Directory.Exists(Path.Combine(factorioDirectory, "data", info.Name)))
                    {
                        return Mod.Create(Path.Combine(factorioDirectory, "data", info.Name));
                    }

                    var startFileName = Path.Combine(factorioModDirectory, info.Name);
                    if (Directory.Exists(startFileName))
                    {
                        return Mod.Create(startFileName);
                    }

                    var matchingDirectory = new DirectoryInfo(factorioModDirectory).EnumerateDirectories()
                        .FirstOrDefault(directoryInfo => directoryInfo.Name.StartsWith(info.Name + "_"));
                    if (matchingDirectory != null)
                    {
                        return Mod.Create(matchingDirectory.FullName);
                    }

                    if (File.Exists(startFileName + ".zip"))
                    {
                        return Mod.Create(startFileName + ".zip");
                    }

                    var matchingFile = new DirectoryInfo(factorioModDirectory).EnumerateFiles()
                        .FirstOrDefault(fileInfo => fileInfo.Name.StartsWith(info.Name + "_") && fileInfo.Extension == ".zip");
                    if (matchingFile != null)
                    {
                        return Mod.Create(matchingFile.FullName);
                    }

                    throw new NotImplementedException();
                }

                realModList.AddRange(modList.Mods.Where(info => info.Enabled).Select(GetModFromInfo));
            }
            else
            {
                realModList.Add(Mod.Create(Path.Combine(factorioDirectory, "data/base")));
            }
            return realModList;
        }

        private static void SortModList(IList<Mod> modList)
        {
            var toSort = new List<Mod>(modList);
            modList.Clear();

            while (toSort.Count > 0)
            {
                var mod = toSort[0];
                AddMod(mod, toSort, modList);
            }
        }

        private static void AddMod(Mod mod, List<Mod> toSort, IList<Mod> modList)
        {
            var dependancies = mod.Dependancies;
            foreach (var dependancy in dependancies)
            {
                var dependantMod = FindDependancy(dependancy, modList);
                if (dependantMod == null)
                {
                    dependantMod = FindDependancy(dependancy, toSort);
                    if (dependantMod == null)
                    {
                        if (dependancy.Type == DependencyType.Hard)
                        {
                            throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        AddMod(dependantMod, toSort, modList);
                    }
                }
            }

            modList.Add(mod);
            toSort.Remove(mod);
        }

        private static Mod FindDependancy(Dependency dependancy, IList<Mod> modList)
        {
            var matchingMod = modList.FirstOrDefault(x => x.InternalName == dependancy.Name);
            if (matchingMod != null)
            {
                if (dependancy.Type == DependencyType.Incompatible)
                {
                    throw new NotImplementedException();
                }

                switch (dependancy.Equality)
                {
                    case DependencyEquality.None:
                        break;

                    case DependencyEquality.LessThan:
                        if (dependancy.Version < matchingMod.Version)
                        {
                            throw new NotImplementedException();
                        }
                        break;

                    case DependencyEquality.LessEquals:
                        if (dependancy.Version <= matchingMod.Version)
                        {
                            throw new NotImplementedException();
                        }
                        break;

                    case DependencyEquality.GreaterThan:
                        if (dependancy.Version > matchingMod.Version)
                        {
                            throw new NotImplementedException();
                        }
                        break;

                    case DependencyEquality.GreaterEquals:
                        if (dependancy.Version >= matchingMod.Version)
                        {
                            throw new NotImplementedException();
                        }
                        break;

                    case DependencyEquality.Equals:
                        if (dependancy.Version == matchingMod.Version)
                        {
                            throw new NotImplementedException();
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return matchingMod;
            }

            return null;
        }
    }

    internal class TableConverter : JsonConverter<LuaTable>
    {
        public override LuaTable Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, LuaTable value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<object, object> entry in value)
            {
                writer.WritePropertyName(entry.Key.ToString());
                if (entry.Value is LuaTable entryTable)
                {
                    Write(writer, entryTable, options);
                }
                else if (entry.Value is string entryString)
                {
                    writer.WriteStringValue(entryString);
                }
                else if (entry.Value is long entryLong)
                {
                    writer.WriteNumberValue(entryLong);
                }
                else if (entry.Value is double entryDouble)
                {
                    if (double.IsInfinity(entryDouble))
                    {
                        writer.WriteStringValue(entryDouble.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        writer.WriteNumberValue(entryDouble);
                    }
                }
                else if (entry.Value is bool entryBoolean)
                {
                    writer.WriteBooleanValue(entryBoolean);
                }
                else if (entry.Value == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            writer.WriteEndObject();
        }
    }

    internal class DoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            if (double.IsInfinity(value))
            {
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                writer.WriteNumberValue(value);
            }
        }
    }
}
