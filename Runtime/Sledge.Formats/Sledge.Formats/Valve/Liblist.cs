using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sledge.Formats.Valve
{
    /// <summary>
    /// The liblist.gam file, used by Goldsource games and mods.
    /// </summary>
    public class Liblist : Dictionary<string, string>
    {
        #region Properties

        /// <summary>
        /// The name of the game/mod.
        /// </summary>
        public string Game
        {
            get => TryGetValue("game", out var s) ? s : null;
            set => this["game"] = value;
        }

        /// <summary>
        /// A path to an uncompressed, 24bit, 16x16 resolution TGA file, relative to the mod directory, with no file extension.
        /// </summary>
        public string Icon
        {
            get => TryGetValue("icon", out var s) ? s : null;
            set => this["icon"] = value;
        }

        /// <summary>
        /// The name of the team or person who created this game/mod.
        /// </summary>
        public string Developer
        {
            get => TryGetValue("developer", out var s) ? s : null;
            set => this["developer"] = value;
        }

        /// <summary>
        /// A URL to the developer's website.
        /// </summary>
        public string DeveloperUrl
        {
            get => TryGetValue("developer_url", out var s) ? s : null;
            set => this["developer_url"] = value;
        }

        /// <summary>
        /// A URL to the game/mod's manual.
        /// </summary>
        public string Manual
        {
            get => TryGetValue("manual", out var s) ? s : null;
            set => this["manual"] = value;
        }

        /// <summary>
        /// The path to the game's DLL file on Windows, relative to the mod directory. e.g. "dlls\hl.dll"
        /// </summary>
        public string GameDll
        {
            get => TryGetValue("gamedll", out var s) ? s : null;
            set => this["gamedll"] = value;
        }

        /// <summary>
        /// The path to the game's DLL file on Linux, relative to the mod directory. e.g. "dlls/hl.so"
        /// </summary>
        public string GameDllLinux
        {
            get => TryGetValue("gamedll_linux", out var s) ? s : null;
            set => this["gamedll_linux"] = value;
        }

        /// <summary>
        /// The path to the game's DLL file on OSX, relative to the mod directory. e.g. "dlls/hl.dylib"
        /// </summary>
        public string GameDllOsx
        {
            get => TryGetValue("gamedll_osx", out var s) ? s : null;
            set => this["gamedll_osx"] = value;
        }
        
        /// <summary>
        /// Enable VAC security.
        /// </summary>
        public bool? Secure
        {
            get => TryGetValue("secure", out var s) && Int32.TryParse(s, out var b) ? b == 1 : (bool?)null;
            set => this["secure"] = !value.HasValue ? null : value.Value ? "1" : "0";
        }

        /// <summary>
        /// If this is a server-only mod.
        /// </summary>
        public bool? ServerOnly
        {
            get => TryGetValue("svonly", out var s) && Int32.TryParse(s, out var b) ? b == 1 : (bool?)null;
            set => this["svonly"] = !value.HasValue ? null : value.Value ? "1" : "0";
        }

        /// <summary>
        /// If the mod requires a new client.dll
        /// </summary>
        public bool? ClientDllRequired
        {
            get => TryGetValue("cldll", out var s) && Int32.TryParse(s, out var b) ? b == 1 : (bool?)null;
            set => this["cldll"] = !value.HasValue ? null : value.Value ? "1" : "0";
        }

        /// <summary>
        /// The type of game/mod. Usually "singleplayer_only" or "multiplayer_only".
        /// </summary>
        public string Type
        {
            get => TryGetValue("type", out var s) ? s : null;
            set => this["type"] = value;
        }

        /// <summary>
        /// The name of the map to load when the player starts a new game, without the extension. e.g. "c0a0"
        /// </summary>
        public string StartingMap
        {
            get => TryGetValue("startmap", out var s) ? s : null;
            set => this["startmap"] = value;
        }

        /// <summary>
        /// The name of the map to load when the player starts the training map, without the extension. e.g. "t0a0"
        /// </summary>
        public string TrainingMap
        {
            get => TryGetValue("trainmap", out var s) ? s : null;
            set => this["trainmap"] = value;
        }

        /// <summary>
        /// The name of the multiplayer entity class.
        /// </summary>
        public string MultiplayerEntity
        {
            get => TryGetValue("mpentity", out var s) ? s : null;
            set => this["mpentity"] = value;
        }

        /// <summary>
        /// Do not show maps with names containing this string in create server dialogue.
        /// </summary>
        public string MultiplayerFilter
        {
            get => TryGetValue("mpfilter", out var s) ? s : null;
            set => this["mpfilter"] = value;
        }

        /// <summary>
        /// The mod/game to base this mod/game off of. e.g. "cstrike"
        /// </summary>
        public string FallbackDirectory
        {
            get => TryGetValue("fallback_dir", out var s) ? s : null;
            set => this["fallback_dir"] = value;
        }

        /// <summary>
        /// True to load maps from the base game/mod.
        /// </summary>
        public bool? FallbackMaps
        {
            get => TryGetValue("fallback_maps", out var s) && Int32.TryParse(s, out var b) ? b == 1 : (bool?)null;
            set => this["fallback_maps"] = !value.HasValue ? null : value.Value ? "1" : "0";
        }

        /// <summary>
        /// Prevent the player model from being anything except player.mdl.
        /// </summary>
        public bool? NoModels
        {
            get => TryGetValue("nomodels", out var s) && Int32.TryParse(s, out var b) ? b == 1 : (bool?)null;
            set => this["nomodels"] = !value.HasValue ? null : value.Value ? "1" : "0";
        }

        /// <summary>
        /// Don't allow HD models.
        /// </summary>
        public bool? NoHighDefinitionModels
        {
            get => TryGetValue("nohimodels", out var s) && Int32.TryParse(s, out var b) ? b == 1 : (bool?)null;
            set => this["nohimodels"] = !value.HasValue ? null : value.Value ? "1" : "0";
        }

        /// <summary>
        /// Use detailed textures.
        /// </summary>
        public bool? DetailedTextures
        {
            get => TryGetValue("detailed_textures", out var s) && Int32.TryParse(s, out var b) ? b == 1 : (bool?)null;
            set => this["detailed_textures"] = !value.HasValue ? null : value.Value ? "1" : "0";
        }

        #endregion

        public Liblist()
        {

        }

        public Liblist(Stream stream)
        {
            using (var sr = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var c = line.IndexOf("//", StringComparison.Ordinal);
                    if (c >= 0) line = line.Substring(0, c);
                    line = line.Trim();

                    if (String.IsNullOrWhiteSpace(line)) continue;

                    c = line.IndexOf(' ');
                    if (c < 0) continue;

                    var key = line.Substring(0, c).ToLower();
                    if (String.IsNullOrWhiteSpace(key)) continue;

                    var value = line.Substring(c + 1);
                    if (value[0] != '"' || value[value.Length - 1] != '"') continue;

                    value = value.Substring(1, value.Length - 2).Trim();
                    this[key] = value;
                }
            }
        }

        public void Write(Stream stream)
        {
            using (var sr = new StreamWriter(stream, Encoding.ASCII, 1024, true))
            {
                foreach (var kv in this)
                {
                    sr.WriteLine($"{kv.Key} \"{kv.Value}\"");
                }
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var kv in this)
            {
                sb.AppendLine($"{kv.Key} \"{kv.Value}\"");
            }
            return sb.ToString();
        }
    }
}
