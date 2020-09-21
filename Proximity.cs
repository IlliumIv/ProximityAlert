using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Proximity
{
    public partial class Proximity : BaseSettingsPlugin<ProximitySettings>
    {
        private IngameState ingameState;
        private static SoundController soundController;
        private RectangleF windowArea;
        private static Dictionary<string, Warning> PathDict = new Dictionary<string, Warning>();
        private static Dictionary<string, Warning> ModDict = new Dictionary<string, Warning>();
        private static string soundDir;
        private static bool PlaySounds = true;
        public override bool Initialise()
        {
            base.Initialise();
            Name = "Proximity Alerts";
            ingameState = GameController.Game.IngameState;
            soundController = GameController.SoundController;
            windowArea = GameController.Window.GetWindowRectangle();
            Graphics.InitImage(Path.Combine(DirectoryFullName, "textures\\Direction-Arrow.png").Replace('\\', '/'), false);
            Graphics.InitImage(Path.Combine(DirectoryFullName, "textures\\back.png").Replace('\\', '/'), false);
            soundDir = Path.Combine(DirectoryFullName, "sounds\\").Replace('\\', '/');
            PathDict = LoadConfig(Path.Combine(DirectoryFullName, "PathAlerts.txt"));
            ModDict = LoadConfig(Path.Combine(DirectoryFullName, "ModAlerts.txt"));

            SetFonts();

            return true;
        }

        public static RectangleF Get64DirectionsUV(double phi, double distance, int rows)
        {
            phi += Math.PI * 0.25; // fix rotation due to projection
            if (phi > 2 * Math.PI) phi -= 2 * Math.PI;

            var xSprite = (float)Math.Round(phi / Math.PI * 32);
            if (xSprite >= 64) xSprite = 0;

            float ySprite = distance > 60 ? distance > 120 ? 2 : 1 : 0;
            var x = xSprite / 64;
            float y = 0;
            if (rows > 0)
            {
                y = ySprite / rows;
                return new RectangleF(x, y, (xSprite + 1) / 64 - x, (ySprite + 1) / rows - y);
            }
            return new RectangleF(x, y, (xSprite + 1) / 64 - x, 1);
        }


        public class Warning
        {
            public string Text { get; set; }
            public Color Color { get; set; }
            public int Distance { get; set; }
            public string SoundFile { get; set; }
        }

        public IEnumerable<string[]> GenDictionary(string path)
        {
            return File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line)
            && line.IndexOf(';') >= 0
            && !line.StartsWith("#")).Select(line => line.Split(new[] { ';' }, 5).Select(parts => parts.Trim()).ToArray());
        }

        public Color HexToColor(string value)
        {
            uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var abgr);
            return Color.FromAbgr(abgr);
        }
        public Dictionary<string, Warning> LoadConfig(string path)
        {
            //if (!File.Exists(path)) CreateConfig(path);
            return GenDictionary(path).ToDictionary(line => line[0], line =>
            {
                int distance = -1;
                int.TryParse(line[3], out distance);
                var preloadAlerConfigLine = new Warning { Text = line[1], Color = HexToColor(line[2]), Distance = distance, SoundFile = line[4] };
                return preloadAlerConfigLine;
            });
        }
        
        public override void EntityAdded(Entity Entity)
        {
            if (!Settings.Enable.Value) return;
            if (Entity.Type == EntityType.Monster) EntityAddedQueue.Enqueue(Entity);
        }

        public override void AreaChange(AreaInstance area)
        {
            try
            {
                EntityAddedQueue.Clear();
                NearbyPaths.Clear();
            }
            catch
            {

            }
        }


        public override Job Tick()
        {
            if (Settings.MultiThreading)
                return GameController.MultiThreadManager.AddJob(TickLogic, nameof(Proximity));
            TickLogic();
            return null;
        }
        private void TickLogic()
        {
            while (EntityAddedQueue.Count > 0)
            {
                var entity = EntityAddedQueue.Dequeue();
                if (entity.IsValid && !entity.IsAlive) continue;
                if (!entity.IsHostile || !entity.IsValid) continue;
                if (!Settings.ShowModAlerts) continue;
                try
                {
                    var rarity = entity.Rarity;
                    var color = Color.Red;
                    if (entity.HasComponent<ObjectMagicProperties>() && entity.IsAlive)
                    {
                        var mods = entity.GetComponent<ObjectMagicProperties>().Mods;
                        if (mods != null)
                        {
                            string modMatch = mods.FirstOrDefault(x => ModDict.ContainsKey(x));
                            var filter = ModDict[modMatch];
                            if (filter != null)
                            {
                                entity.SetHudComponent(new ProximityAlert(entity, filter.Color, filter.Distance, filter.SoundFile));
                                lock (_locker) PlaySound(filter.SoundFile);
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                }
                catch { }
            }
            // Update valid
            foreach (var entity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster])
            {
                var drawCmd = entity.GetHudComponent<ProximityAlert>();
                drawCmd?.Update();
            }
            // Nearby debug stuff
            /*
            if (Settings.ShowNearbyKey.PressedOnce())
            {
                if (NearbyPaths.Count > 0) NearbyPaths.Clear();
                else
                {
                    NearbyPaths.Clear();
                    foreach (var entity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster])
                    {

                        if (entity.DistancePlayer < 200 && entity.IsAlive)
                        {
                            NearbyPaths.Add(new StaticEntity(entity.Path, entity.Pos));
                        }
                    }
                }
            }
            */
        }

        private class SoundStatus
        {
            public SoundStatus(Entity Entity, string sound)
            {
                this.Entity = Entity;
                if (!Played && Entity.IsValid)
                {
                    lock (_locker) PlaySound(sound);
                    Played = true;                    
                }
                return;
            }
            public void Invalid()
            {
                if (Played && !Entity.IsValid)
                {
                    Played = false;
                }
                return;
            }
            public bool PlayedCheck()
            {
                return Played;
            }
            public Entity Entity { get; }
            public bool Played { get; set; }
        }

        private class ProximityAlert
        {
            public ProximityAlert(Entity Entity, Color color, int dist, string sound)
            {
                this.Entity = Entity;
                Color = color;
                Name = string.Empty;
                IsAlive = true;
                Distance = dist;
                SoundFile = sound;
                playWarning = true;
            }

            public Entity Entity { get; }
            public string Name { get; private set; }
            public Color Color { get; private set; }
            public bool IsAlive { get; private set; }
            public int Distance { get; set; }
            public string SoundFile { get; set; }
            public bool playWarning { get; set; }

            public void Update()
            {
                if (!Entity.IsValid) playWarning = true;
                IsAlive = Entity.IsValid && Entity.IsAlive;
                if (Entity.HasComponent<ObjectMagicProperties>() && Entity.IsAlive)
                {
                    var mods = Entity.GetComponent<ObjectMagicProperties>()?.Mods ?? null;
                    if (mods != null && mods.Count > 0)
                    {
                        string modMatch = mods.FirstOrDefault(x => ModDict.ContainsKey(x));
                        var filter = ModDict[modMatch];
                        if (filter != null)
                        {
                            Name = filter.Text;
                            Color = filter.Color;
                            Distance = filter.Distance;
                            SoundFile = filter.SoundFile;
                            if (playWarning)
                            {
                                lock (_locker)
                                    PlaySound(filter.SoundFile);
                            }
                            playWarning = false;
                            return;
                        }
                    }
                }
            }
        }

        private readonly Queue<Entity> EntityAddedQueue = new Queue<Entity>();
        private static DateTime LastPlayed;
        private static readonly object _locker = new object();

        private static void PlaySound(string path)
        {
            if (!PlaySounds) return;
            // Sanity Check because I'm too lazy to make a queue
            if ((DateTime.Now - LastPlayed).TotalMilliseconds > 250)
            {
                if (path != string.Empty) soundController.PlaySound(Path.Combine(soundDir, path).Replace('\\', '/'));
                LastPlayed = DateTime.Now;
            }
        }

        private class StaticEntity
        {            
            public StaticEntity(string path, Vector3 pos)
            {
                Path = path;
                Pos = pos;
            }
            public String Path { get; private set; }
            public Vector3 Pos { get; private set; }
        }
        private static List<StaticEntity> NearbyPaths = new List<StaticEntity>();
        public override void Render()
        {
            try
            {
                PlaySounds = Settings.PlaySounds;
                var _height = (float)Int32.Parse(Settings.Font.Value.Substring(Settings.Font.Value.Length - 2));
                _height = _height * Settings.Scale;
                var margin = (_height / Settings.Scale) / 4;

                if (!Settings.Enable) return;
                    if (Settings.ShowNearby)
                    {
                        foreach (var sEnt in NearbyPaths)
                        {
                            var entityScreenPos = ingameState.Camera.WorldToScreen(sEnt.Pos.Translate(0, 0, 0));
                            var textWidth = Graphics.MeasureText(sEnt.Path, 10) * 0.73f;
                            Graphics.DrawBox(new RectangleF(entityScreenPos.X - (textWidth.X/2),entityScreenPos.Y - 7,textWidth.X,13),new Color(0,0,0,200));
                            Graphics.DrawText(sEnt.Path, new System.Numerics.Vector2(entityScreenPos.X, entityScreenPos.Y), Color.White, 10, Settings.Font.Value, FontAlign.Center | FontAlign.VerticalCenter);
                            // Graphics.DrawText(sEnt.Path, new System.Numerics.Vector2(entityScreenPos.X, entityScreenPos.Y), Color.White, 10, "FrizQuadrataITC:13", FontAlign.Center | FontAlign.VerticalCenter);

                        }
                    }
                if(Settings.ShowSirusLine)
                    foreach(var sEnt in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster].Where(x => x.Metadata.Equals("Metadata/Monsters/AtlasExiles/AtlasExile5")))
                    {
                        if (sEnt.Path.Contains("Throne") || sEnt.Path.Contains("Apparation")) break;
                        if (sEnt.DistancePlayer > 200) break;
                        var entityScreenPos = ingameState.Camera.WorldToScreen(sEnt.Pos.Translate(0, 0, 0));
                        var playerPosition = GameController.Game.IngameState.Camera.WorldToScreen(GameController.Player.Pos);
                        Graphics.DrawLine(playerPosition, entityScreenPos, 4, new Color(255, 0, 255, 140));

                        Graphics.DrawText(sEnt.DistancePlayer.ToString(), new Vector2(0, 0));
                    }

                string unopened = "";
                string mods = "";
                int lines = 0;
                
                Vector2 origin = windowArea.Center.Translate(Settings.ProximityX - 96, Settings.ProximityY);

                // mod Alerts
                foreach (var entity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster])
                {
                    string tempPath = entity.Path;
                    if (tempPath.Contains("@")) tempPath = tempPath.Split('@')[0];
                    // skip white mobs
                    if (entity.Rarity == MonsterRarity.White) continue;
                    var structValue = entity.GetHudComponent<ProximityAlert>();
                    if (structValue == null || !entity.IsAlive || structValue.Name == string.Empty) continue;
                    Vector2 delta = entity.GridPos - GameController.Player.GridPos;
                    double phi;
                    double distance = delta.GetPolarCoordinates(out phi);

                    RectangleF rectDirection = new RectangleF(origin.X - margin - _height / 2, origin.Y - margin / 2 - _height - (lines * _height), _height, _height);
                    var rectUV = Get64DirectionsUV(phi, distance, 3);

                    if (!mods.Contains(structValue.Name))
                    {
                        mods += structValue.Name;
                        lines++;
                        Graphics.DrawText(structValue.Name, new System.Numerics.Vector2(origin.X + _height / 2, origin.Y - (lines * _height)), structValue.Color, 10, Settings.Font.Value, FontAlign.Left);
                        // Graphics.DrawText(structValue.Name, new System.Numerics.Vector2(origin.X + 4, origin.Y - (lines * 15)), structValue.Color, 10, "FrizQuadrataITC:15", FontAlign.Left);
                        Graphics.DrawImage("Direction-Arrow.png", rectDirection, rectUV, structValue.Color);
                    }

                }

                // entities
                foreach (var entity in GameController.EntityListWrapper.Entities
                    .Where(x => x.Type == EntityType.Chest || x.Type == EntityType.Monster || x.Type == EntityType.IngameIcon))
                {
                    string tempPath = entity.Path;
                    if (tempPath.Contains("@")) tempPath = tempPath.Split('@')[0];

                    bool match = false;
                    Color lineColor = Color.White;
                    String lineText = "";
                    if (entity.HasComponent<Chest>() && entity.IsOpened) continue;
                    if (entity.HasComponent<Monster>() && (!entity.IsAlive || !entity.IsValid)) continue;
                    if (entity.GetHudComponent<SoundStatus>() != null && !entity.IsValid) entity.GetHudComponent<SoundStatus>().Invalid();
                    if (entity.Type == EntityType.IngameIcon && (!entity.IsValid || (entity?.GetComponent<MinimapIcon>().IsHide ?? true))) continue;
                    Vector2 delta = entity.GridPos - GameController.Player.GridPos;
                    double phi;
                    double distance = delta.GetPolarCoordinates(out phi);

                    RectangleF rectDirection = new RectangleF(origin.X - margin - _height/2, origin.Y - margin/2 - _height - (lines * _height), _height, _height);
                    var rectUV = Get64DirectionsUV(phi, distance, 3);
                    string ePath = entity.Path;
                    // prune paths where relevant
                    if (ePath.Contains("@")) ePath = ePath.Split('@')[0];
                    // Hud component check
                    var structValue = entity.GetHudComponent<ProximityAlert>();
                    if (structValue != null && !mods.Contains(structValue.Name))
                    {
                        mods += structValue.Name;
                        lines++;
                        Graphics.DrawText(structValue.Name, new System.Numerics.Vector2(origin.X + _height / 2, origin.Y - (lines * _height)), structValue.Color, 10, Settings.Font.Value, FontAlign.Left);
                        // Graphics.DrawText(structValue.Name, new System.Numerics.Vector2(origin.X + 4, origin.Y - (lines * 15)), structValue.Color, 10, "FrizQuadrataITC:15", FontAlign.Left);
                        Graphics.DrawImage("Direction-Arrow.png", rectDirection, rectUV, structValue.Color);
                        match = true;
                    }
                    // Rogue Exiles
                    if (ePath.StartsWith("Metadata/Monsters/Exiles/Exile"))
                    {
                        lineText = "Rogue Exile";
                        lineColor = new Color(254, 192, 118);
                        match = true;
                        lines++;
                    }
                    // Contains Check
                    if (!match)
                        foreach (var filterEntry in PathDict.Where(x => ePath.Contains(x.Key)).Take(1))
                        {
                            var filter = filterEntry.Value;
                            unopened = $"{filter.Text}\n{unopened}";
                            if (filter.Distance == -1 || (filter.Distance == -2 && entity.IsValid) || distance < filter.Distance)
                            {
                                var soundStatus = entity.GetHudComponent<SoundStatus>() ?? null;
                                if (soundStatus == null || !soundStatus.PlayedCheck()) entity.SetHudComponent(new SoundStatus(entity, filter.SoundFile));
                                lineText = filter.Text;
                                lineColor = filter.Color;
                                match = true;
                                lines++;
                                break;
                            }
                        }
                    // Hardcoded Chests
                    if (!match)
                    {
                        if (entity.HasComponent<Chest>() && ePath.Contains("Delve"))
                        {
                                    string chestName = Regex.Replace(Path.GetFileName(ePath), @"((?<=\p{Ll})\p{Lu})|((?!\A)\p{Lu}(?>\p{Ll}))", " $0")
                                        .Replace("Delve Chest ", String.Empty)
                                        .Replace("Delve Azurite ", "Azurite ")
                                        .Replace("Delve Mining Supplies ", String.Empty)
                                        .Replace("_", String.Empty);
                                    if (chestName.EndsWith(" Encounter") || chestName.EndsWith(" No Drops")) continue;
                                    if (distance > 100)
                                    {
                                        if (chestName.Contains("Generic")
                                            || chestName.Contains("Vein")
                                            || chestName.Contains("Flare")
                                            || chestName.Contains("Dynamite")
                                            || chestName.Contains("Armour")
                                            || chestName.Contains("Weapon"))
                                            if (chestName.Contains("Path ") || !chestName.Contains("Currency"))
                                                continue;
                                    }
                                    if (chestName.Contains("Currency") || chestName.Contains("Fossil")) lineColor = new Color(255, 0, 255);
                                    if (chestName.Contains("Flares")) lineColor = new Color(0, 200, 255);
                                    if (chestName.Contains("Dynamite") || chestName.Contains("Explosives")) lineColor = new Color(255, 50, 50);
                                    lineText = chestName;
                                    lines++;
                                    match = true;
                        }
                    }
                    if (match)
                    {
                        Graphics.DrawText(lineText, new System.Numerics.Vector2(origin.X + _height/2, origin.Y - (lines * _height)), lineColor, 10, Settings.Font.Value, FontAlign.Left);
                        // Graphics.DrawText(lineText, new System.Numerics.Vector2(origin.X + 4, origin.Y - (lines * 15)), lineColor, 10, "FrizQuadrataITC:15", FontAlign.Left);
                        Graphics.DrawImage("Direction-Arrow.png", rectDirection, rectUV, lineColor);

                    }
                }

                if (lines > 0)
                {
                    var widthMultiplier = 1 + _height / 100;

                    RectangleF box = new RectangleF(origin.X - 2, origin.Y - margin - (lines * _height), (192 + 4) * widthMultiplier, margin + (lines * _height) + 4);
                    Graphics.DrawImage("back.png", box, Color.White);
                    Graphics.DrawLine(new Vector2(origin.X - 15, origin.Y - margin - (lines * _height)), new Vector2(origin.X + (192 + 4) * widthMultiplier, origin.Y - margin - (lines * _height)), 1, Color.White);
                    Graphics.DrawLine(new Vector2(origin.X - 15, origin.Y + 3), new Vector2(origin.X + (192 + 4) * widthMultiplier, origin.Y + 3), 1, Color.White);
                }
            }
            catch { }
        }
    }
}
