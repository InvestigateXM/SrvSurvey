﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SrvSurvey.net;
using SrvSurvey.units;
using System;

namespace SrvSurvey.game
{
    [JsonConverter(typeof(BoxelSearchJsonConverter))]
    internal class BoxelSearch
    {
        private static Dictionary<string, int> cacheCounts = new();

        public event MethodInvoker? changed;

        #region saved data members 

        /// <summary> If the player is actively searching a boxel </summary>
        public bool active;

        /// <summary> The generic name of the top-level boxel to search </summary>
        public Boxel boxel;

        /// <summary> When we first started searching the top boxel </summary>
        public DateTime startedOn;

        /// <summary> The name of the current (contained?) boxel we are searching </summary>
        public Boxel current;

        /// <summary> Count of systems expected in this boxel </summary>
        public int currentCount { get; private set; }

        /// <summary> If next system name should be copied to the clipboard when entering gal-map </summary>
        public bool autoCopy = true;

        /// <summary> If the list was collapsed in the boxel search window </summary>
        public bool collapsed;

        /// <summary> Mark systems as pre-complete if we visited them before starting this search </summary>
        public bool skipAlreadyVisited;

        /// <summary> Mark systems as pre-complete if Spansh knows them from before starting this search</summary>
        public bool skipKnownToSpansh;

        /// <summary> How low to go?</summary>
        public MassCode lowMassCode = 'c';

        #endregion


        /// <summary> The progress for all contained/potential boxels, with a count of systems for each, -1 if empty or 0 if unknown </summary>
        [JsonIgnore]
        private Dictionary<Boxel, int> progress = new();

        /// <summary> Lowest known system number in the current boxel </summary>
        [JsonIgnore]
        public int currentMin;

        /// <summary> Highest known system number in the current boxel </summary>
        [JsonIgnore]
        public int currentMax;

        /// <summary> if the current boxel has been marked as empty </summary>
        [JsonIgnore]
        public bool currentEmpty;

        /// <summary> Known systems in the current boxel </summary>
        [JsonIgnore]
        public HashSet<System> systems = new HashSet<System>(new System.Comparer());

        /// <summary> The set of contained boxels known to be empty </summary>
        [JsonIgnore]
        private HashSet<string>? emptyBoxels;

        /// <summary> The file used to store empty boxels </summary>
        private string? emptyBoxelsFilepath;

        /// <summary> The count of systems visited in the current boxel </summary>
        [JsonIgnore]
        public int countVisited => systems.Count(s => s.complete);

        public void reset(Boxel newBoxel, bool active)
        {
            // activate the feature
            this.active = active;

            if (boxel != newBoxel || startedOn == DateTime.MinValue)
                startedOn = DateTime.Now;

            boxel = newBoxel;

            // prep progress with all children
            progress = new();

            Action<Boxel> func = null!;
            func = (Boxel bx) =>
            {
                progress[bx] = 0;
                if (bx.massCode > lowMassCode)
                    bx.children.ForEach(child => func(child));
            };
            func(boxel);
            Game.log($"Boxel progress count: {progress.Count}");
        }

        public override string ToString()
        {
            return currentEmpty
                ? $"{current} (empty)"
                : $"{current} ({currentCount}/{currentMin}/{currentMax})";
        }

        public void setCurrentCount(int n)
        {
            if (current == null) return;

            this.currentCount = n;
            cacheCounts[current.name] = n;
        }

        /// <summary> Set the current boxel and start searching for systems it may contain </summary>
        public void setCurrent(Boxel bx, bool force = false)
        {
            if (this.current == bx && !force) return;

            this.systems.Clear();
            this.current = bx;

            // is this a previously empty boxel?
            this.emptyBoxels ??= this.loadEmptyBoxels();
            if (this.emptyBoxels!.Contains(bx.id))
            {
                this.currentEmpty = true;
                this.fireChanged();
            }
            else
            {
                if (cacheCounts.ContainsKey(bx.name))
                    this.currentCount = cacheCounts[bx.name];
                else
                    this.currentCount = 1;
                this.currentEmpty = false;
            }

            if (force)
                fireChanged();

            // now start looking for systems on disk and from Spansh
            Task.Run(this.findSystemsInCurrentBoxel);
        }

        private async Task findSystemsInCurrentBoxel()
        {
            try
            {
                // start an async spansh query for systems whose name starts with the boxel prefix, we'll process the results below
                var query = current.prefix + "*";
                var taskSpansh = Game.spansh.getBoxelSystems(query);

                // look for systems we have personally visited or are about to visit
                var dirty = false;
                dirty |= this.findSystemsFromDisk();
                dirty |= this.findSystemsFromNavRoute(Game.activeGame?.navRoute?.Route);

                // if the spansh results are still pending, give something to the UX
                if (dirty && !taskSpansh.IsCompleted)
                {
                    this.fireChanged();
                    dirty = false;
                }

                // now we wait and process data from spansh
                var spanshResponse = await taskSpansh;
                dirty |= this.findSystemsFromSpansh(spanshResponse);

                if (dirty)
                    this.fireChanged();
            }
            catch (Exception ex)
            {
                FormErrorSubmit.Show(ex);
            }
        }

        private System findOrAddSystem(Boxel bx)
        {
            var match = systems.FirstOrDefault(sys => sys.name == bx.name);
            if (match == null)
            {
                match = new System(bx);
                systems.Add(match);
            }

            return match;
        }

        private bool findSystemsFromDisk()
        {
            Game.log($"Searching disk for star systems: {current.prefix}*");

            // search for system with suitable filenames
            var folder = Path.Combine(Program.dataFolder, "systems", CommanderSettings.currentOrLastFid);
            Directory.CreateDirectory(folder);

            var files = Directory.GetFiles(folder, $"{current.prefix}*.json");
            if (files.Length == 0) return false;

            progress[current] = files.Length;

            var dirty = false;
            foreach (var file in files)
            {
                var fileData = Data.Load<SystemData>(file)!;
                var bx = Boxel.parse(fileData.name);
                if (bx == null || bx.prefix != this.current.prefix) continue;
                dirty = true;

                // merge data
                var sys = findOrAddSystem(bx);
                sys.complete |= fileData.lastVisited > startedOn || this.skipAlreadyVisited;
                sys.starPos ??= fileData.starPos;

                if (sys.visitedAt == null || sys.visitedAt < fileData.lastVisited)
                    sys.visitedAt = fileData.lastVisited;
            }

            return dirty;
        }

        private bool findSystemsFromNavRoute(List<RouteEntry>? route)
        {
            if (route == null) return false;

            var dirty = false;
            foreach (var hop in route)
            {
                var bx = Boxel.parse(hop.StarSystem);
                if (bx == null || bx.prefix != this.current.prefix) continue;
                dirty = true;

                // merge data
                var sys = findOrAddSystem(bx);
                sys.starPos ??= hop.StarPos;
            }

            return dirty;
        }

        private bool findSystemsFromSpansh(Spansh.SystemResponse response)
        {
            var dirty = false;
            foreach (var result in response.results)
            {
                var bx = Boxel.parse(result.name);
                if (bx == null || bx.prefix != this.current.prefix) continue;
                dirty = true;

                // merge data
                var sys = findOrAddSystem(bx);
                sys.starPos ??= result.toStarPos();

                // require bodies to be known - to guard against systems found from NavRoutes (and nothing else is known)
                if (result.bodies?.Count > 0)
                {
                    sys.spanshUpdated = result.updated_at;
                    // If Spansh was updated after the cmdr started searching but they didn't visit it personally - consider this not complete
                    sys.complete |= result.updated_at < startedOn && this.skipKnownToSpansh;
                }
            }

            return dirty;
        }

        private void fireChanged()
        {
            // get the highest and lowest n2 values
            currentMin = -1;
            currentMax = 0;
            foreach (var sys in systems)
            {
                var n2 = sys.name.n2;
                if (n2 < currentMin || currentMin == -1) currentMin = n2;
                if (n2 > currentMax) currentMax = n2;
            }

            // fire event on the main thread
            if (this.changed != null)
                Program.control.Invoke(() => this.changed());
        }

        public void toggleEmpty()
        {
            // store empty boxels in a separate file, grouped at the mass code g level to avoid files getting too large.
            // to save some space, we store just the id part, not the sector or n2
            if (current.massCode == 'h') return;

            // load from file if first time
            if (this.emptyBoxels == null)
                this.emptyBoxels = loadEmptyBoxels();

            var dirty = false;
            if (!currentEmpty)
            {
                this.currentEmpty = true;
                this.progress[current] = -1;
                dirty = this.emptyBoxels.Add(current.id);
            }
            else
            {
                this.currentEmpty = false;
                this.progress[current] = 0;
                dirty = this.emptyBoxels.Remove(current.id);

                this.currentCount = cacheCounts.ContainsKey(current.name)
                    ? cacheCounts[current.name]
                    : this.currentCount = 1;
            }

            if (dirty)
            {
                Game.log($"Updating: .\\emptyBoxels\\{emptyBoxelsFilepath}");
                var json = JsonConvert.SerializeObject(this.emptyBoxels);
                File.WriteAllText(emptyBoxelsFilepath!, json);
            }

            this.fireChanged();
        }

        private HashSet<string> loadEmptyBoxels()
        {
            if (emptyBoxelsFilepath == null) prepEmptyBoxelFilePath();

            if (!File.Exists(emptyBoxelsFilepath))
                return new HashSet<string>();

            var json = File.ReadAllText(emptyBoxelsFilepath);
            return JsonConvert.DeserializeObject<HashSet<string>>(json)!;
        }

        private void prepEmptyBoxelFilePath()
        {
            if (current.massCode == 'h')
                throw new Exception("Empty boxels are not stored at mass-code: h");

            if (this.emptyBoxelsFilepath != null) return;

            var bx = current.to(0);
            while (bx.massCode < 'g')
                bx = bx.parent;

            var filename = $"{bx.name}.json";
            var folder = Path.Combine(Program.dataFolder, "emptyBoxels");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            this.emptyBoxelsFilepath = Path.Combine(folder, filename);
            Game.log($"Empty boxels filepath: {emptyBoxelsFilepath}");
        }

        public void markVisited(string name, StarPos pos)
        {
            var bx = Boxel.parse(name);
            if (bx == null || bx.prefix != this.current.prefix)
            {
                this.fireChanged();
                return;
            }

            var match = findOrAddSystem(bx);
            if (match == null) return;

            Game.log($"Updating boxel search: system: {match.name}");
            match.starPos ??= pos;
            match.complete = true;
            match.visitedAt = DateTime.UtcNow;

            this.fireChanged();
        }

        public string getNextToVisit()
        {
            // use prefix if nothing yet visited - to help find the count of systems in this boxel
            if (!currentEmpty && systems.All(s => !s.complete))
                return current.prefix;

            // take the first incomplete system
            var nextSys = systems.FirstOrDefault(sys => !sys.complete);
            var next = nextSys?.name.name;

            // take the first unknown system
            if (next == null)
            {
                for (int n = 0; n < currentCount; n++)
                {
                    if (systems.Any(sys => n == sys.name.n2 && sys.complete)) continue;
                    next = current.to(n);
                    break;
                }
            }

            // suggest another boxel in same mass-code?
            if (next == null && progress?.Count > 0)
            {
                var mc = boxel.massCode;
                do
                {
                    next = progress.FirstOrDefault(p => p.Value == 0 && p.Key.massCode == mc).Key?.prefix;
                    if (mc == 'a') break;
                    mc--;
                } while (next == null && mc >= lowMassCode);
            }


            return next ?? current.prefix;
        }

        public void updateFromRoute(List<RouteEntry> navRoute)
        {
            var dirty = findSystemsFromNavRoute(navRoute);
            if (dirty)
                this.fireChanged();
        }

        /// <summary>
        /// Calculate progress across all boxels contained in the top-level one
        /// </summary>
        public string calcProgress()
        {
            this.emptyBoxels ??= this.loadEmptyBoxels();

            // total count of boxels
            var mc = boxel.massCode;
            var total = 0;
            while (mc >= lowMassCode)
            {
                total += (int)Math.Pow(8, (int)boxel.massCode - mc);
                mc--;
            }

            var count = getConfirmedCount(boxel);

            // walk all contained boxels, looking for local files or emptyBoxel entries
            return $"{count} of {total}";
        }

        private int getConfirmedCount(Boxel bx)
        {
            var count = 0;

            // marked empty?
            if (this.emptyBoxels!.Contains(bx.id))
                progress[bx] = -1;

            var sysCount = progress.GetValueOrDefault(bx);

            if (sysCount == 0)
            {
                // search for systems with suitable filenames
                var folder = Path.Combine(Program.dataFolder, "systems", CommanderSettings.currentOrLastFid);
                sysCount = Directory.GetFiles(folder, $"{bx.prefix}*.json").Length;
                progress[bx] = sysCount;
            }

            if (sysCount != 0)
                count++;

            // now increment with child progress
            if (bx.massCode > lowMassCode)
                foreach (var child in bx.children)
                    count += getConfirmedCount(child);

            return count;
        }

        /// <summary> Represents a system to be visited within a Boxel </summary>
        public class System
        {
            public Boxel name;
            public bool complete;
            public StarPos? starPos;
            public DateTimeOffset? visitedAt;
            public DateTimeOffset? spanshUpdated;

            public System(Boxel name, StarPos? starPos = null)
            {
                this.name = name;
                this.starPos = starPos;
            }

            public override string ToString()
            {
                return $"{name}:{complete}";
            }

            /// <summary> Returns true is the star pos is known for this system </summary>
            [JsonIgnore]
            public bool isKnown => starPos != null && starPos?.x != 0 && starPos?.y != 0 && starPos?.z != 0;

            public class Comparer : IEqualityComparer<System>
            {
                bool IEqualityComparer<System>.Equals(System? x, System? y)
                {
                    return x?.name == y?.name;
                }

                int IEqualityComparer<System>.GetHashCode(System obj)
                {
                    return obj.name.GetHashCode();
                }
            }
        }
    }

    class BoxelSearchJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return false;
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            try
            {
                var obj = serializer.Deserialize<JToken>(reader);
                if (obj == null || !obj.HasValues) return null;

                // read the simple fields
                var bs = new BoxelSearch
                {
                    active = obj["active"]?.Value<bool>() ?? false,
                    startedOn = obj["startedOn"]?.Value<DateTime>() ?? DateTime.MinValue,
                    boxel = Boxel.parse(obj["boxel"]?.Value<string>())!,
                    current = Boxel.parse(obj["current"]?.Value<string>())!,

                    autoCopy = obj["autoCopy"]?.Value<bool>() ?? false,
                    collapsed = obj["collapsed"]?.Value<bool>() ?? false,
                    skipAlreadyVisited = obj["skipAlreadyVisited"]?.Value<bool>() ?? false,
                    skipKnownToSpansh = obj["skipKnownToSpansh"]?.Value<bool>() ?? false,
                };

                var n = obj["currentCount"]?.Value<int>() ?? 0;
                bs.setCurrentCount(n);

                return bs;
            }
            catch (Exception ex)
            {
                Game.log($"Failed to parse BoxelSearch: ${ex}");
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var bs = value as BoxelSearch;
            if (bs == null) throw new Exception($"Unexpected type: {value?.GetType().Name}");

            var obj = new JObject
                {
                    { "active", bs.active},
                    { "startedOn", bs.startedOn },
                    { "boxel", bs.boxel?.name },
                    { "current", bs.current?.name },
                    { "currentCount", bs.currentCount},

                    { "autoCopy", bs.autoCopy},
                    { "collapsed", bs.collapsed},
                    { "skipAlreadyVisited", bs.skipAlreadyVisited},
                    { "skipKnownToSpansh", bs.skipKnownToSpansh},
                };

            obj.WriteTo(writer);
        }
    }
}
