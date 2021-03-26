using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
// ReSharper disable CollectionNeverUpdated.Local

namespace ProximityAlert
{
    public partial class Proximity : BaseSettingsPlugin<ProximitySettings>
    {
        private static SoundController _soundController;
        private static Dictionary<string, Warning> _pathDict = new Dictionary<string, Warning>();
        private static Dictionary<string, Warning> _modDict = new Dictionary<string, Warning>();
        private static string _soundDir;
        private static bool _playSounds = true;
        private static DateTime _lastPlayed;
        private static readonly object Locker = new object();
        private static readonly List<StaticEntity> NearbyPaths = new List<StaticEntity>();
        private readonly Queue<Entity> _entityAddedQueue = new Queue<Entity>();
        private IngameState _ingameState;
        private RectangleF _windowArea;

        public override bool Initialise()
        {
            base.Initialise();
            Name = "Proximity Alerts";
            _ingameState = GameController.Game.IngameState;
            lock (Locker) _soundController = GameController.SoundController;
            _windowArea = GameController.Window.GetWindowRectangle();
            Graphics.InitImage(Path.Combine(DirectoryFullName, "textures\\Direction-Arrow.png").Replace('\\', '/'),
                false);
            Graphics.InitImage(Path.Combine(DirectoryFullName, "textures\\back.png").Replace('\\', '/'), false);
            lock (Locker) _soundDir = Path.Combine(DirectoryFullName, "sounds\\").Replace('\\', '/');
            _pathDict = LoadConfig(Path.Combine(DirectoryFullName, "PathAlerts.txt"));
            _modDict = LoadConfig(Path.Combine(DirectoryFullName, "ModAlerts.txt"));
            SetFonts();
            return true;
        }

        private static RectangleF Get64DirectionsUV(double phi, double distance, int rows)
        {
            phi += Math.PI * 0.25; // fix rotation due to projection
            if (phi > 2 * Math.PI) phi -= 2 * Math.PI;

            var xSprite = (float) Math.Round(phi / Math.PI * 32);
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

        private IEnumerable<string[]> GenDictionary(string path)
        {
            return File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line)
                                                         && line.IndexOf(';') >= 0
                                                         && !line.StartsWith("#")).Select(line =>
                line.Split(new[] {';'}, 5).Select(parts => parts.Trim()).ToArray());
        }

        private static Color HexToColor(string value)
        {
            uint.TryParse(value, NumberStyles.HexNumber, null, out var abgr);
            return Color.FromAbgr(abgr);
        }

        private Dictionary<string, Warning> LoadConfig(string path)
        {
            //if (!File.Exists(path)) CreateConfig(path);
            return GenDictionary(path).ToDictionary(line => line[0], line =>
            {
                var distance = -1;
                if (int.TryParse(line[3], out var tmp)) distance = tmp;
                var preloadAlertConfigLine = new Warning
                    {Text = line[1], Color = HexToColor(line[2]), Distance = distance, SoundFile = line[4]};
                return preloadAlertConfigLine;
            });
        }

        public override void EntityAdded(Entity entity)
        {
            if (!Settings.Enable.Value) return;
            if (entity.Type == EntityType.Monster) _entityAddedQueue.Enqueue(entity);
        }

        public override void AreaChange(AreaInstance area)
        {
            try
            {
                _entityAddedQueue.Clear();
                NearbyPaths.Clear();
            }
            catch
            {
                // ignored
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
            while (_entityAddedQueue.Count > 0)
            {
                var entity = _entityAddedQueue.Dequeue();
                if (entity.IsValid && !entity.IsAlive) continue;
                if (!entity.IsHostile || !entity.IsValid) continue;
                if (!Settings.ShowModAlerts) continue;
                try
                {
                    if (entity.HasComponent<ObjectMagicProperties>() && entity.IsAlive)
                    {
                        var mods = entity.GetComponent<ObjectMagicProperties>().Mods;
                        if (mods != null)
                        {
                            var modMatch = mods.FirstOrDefault(x => _modDict.ContainsKey(x));
                            var filter = _modDict[modMatch ?? string.Empty];
                            if (filter != null)
                            {
                                entity.SetHudComponent(new ProximityAlert(entity, filter.Color));
                                lock (Locker)
                                {
                                    PlaySound(filter.SoundFile);
                                }
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }

            // Update valid
            foreach (var entity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster])
            {
                var drawCmd = entity.GetHudComponent<ProximityAlert>();
                drawCmd?.Update();
            }
        }

        private static void PlaySound(string path)
        {
            if (!_playSounds) return;
            // Sanity Check because I'm too lazy to make a queue
            if ((DateTime.Now - _lastPlayed).TotalMilliseconds > 250)
            {
                if (path != string.Empty) _soundController.PlaySound(Path.Combine(_soundDir, path).Replace('\\', '/'));
                _lastPlayed = DateTime.Now;
            }
        }

        public override void Render()
        {
            try
            {
                _playSounds = Settings.PlaySounds;
                var height = (float) int.Parse(Settings.Font.Value.Substring(Settings.Font.Value.Length - 2));
                height = height * Settings.Scale;
                var margin = height / Settings.Scale / 4;

                if (!Settings.Enable) return;
                if (Settings.ShowPathAlerts)
                    foreach (var sEnt in NearbyPaths)
                    {
                        var entityScreenPos = _ingameState.Camera.WorldToScreen(sEnt.Pos.Translate(0, 0, 0));
                        var textWidth = Graphics.MeasureText(sEnt.Path, 10) * 0.73f;
                        Graphics.DrawBox(
                            new RectangleF(entityScreenPos.X - textWidth.X / 2, entityScreenPos.Y - 7, textWidth.X, 13),
                            new Color(0, 0, 0, 200));
                        Graphics.DrawText(sEnt.Path, new Vector2(entityScreenPos.X, entityScreenPos.Y), Color.White, 10,
                            Settings.Font.Value, FontAlign.Center | FontAlign.VerticalCenter);
                        // Graphics.DrawText(sEnt.Path, new System.Numerics.Vector2(entityScreenPos.X, entityScreenPos.Y), Color.White, 10, "FrizQuadrataITC:13", FontAlign.Center | FontAlign.VerticalCenter);
                    }

                if (Settings.ShowSirusLine)
                    foreach (var sEnt in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                        .Where(x => x.Metadata.Equals("Metadata/Monsters/AtlasExiles/AtlasExile5")))
                    {
                        if (sEnt.Path.Contains("Throne") || sEnt.Path.Contains("Apparation")) break;
                        if (sEnt.DistancePlayer > 200) break;
                        var entityScreenPos = _ingameState.Camera.WorldToScreen(sEnt.Pos.Translate(0, 0, 0));
                        var playerPosition =
                            GameController.Game.IngameState.Camera.WorldToScreen(GameController.Player.Pos);
                        Graphics.DrawLine(playerPosition, entityScreenPos, 4, new Color(255, 0, 255, 140));

                        Graphics.DrawText(sEnt.DistancePlayer.ToString(CultureInfo.InvariantCulture), new SharpDX.Vector2(0, 0));
                    }

                var unopened = "";
                var mods = "";
                var lines = 0;

                var origin = _windowArea.Center.Translate(Settings.ProximityX - 96, Settings.ProximityY);

                // mod Alerts
                foreach (var entity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster])
                {
                    // skip white mobs
                    if (entity.Rarity == MonsterRarity.White) continue;
                    var structValue = entity.GetHudComponent<ProximityAlert>();
                    if (structValue == null || !entity.IsAlive || structValue.Name == string.Empty) continue;
                    var delta = entity.GridPos - GameController.Player.GridPos;
                    var distance = delta.GetPolarCoordinates(out var phi);

                    var rectDirection = new RectangleF(origin.X - margin - height / 2,
                        origin.Y - margin / 2 - height - lines * height, height, height);
                    var rectUV = Get64DirectionsUV(phi, distance, 3);

                    if (!mods.Contains(structValue.Name))
                    {
                        mods += structValue.Name;
                        lines++;
                        Graphics.DrawText(structValue.Name,
                            new Vector2(origin.X + height / 2, origin.Y - lines * height), structValue.Color, 10,
                            Settings.Font.Value);
                        // Graphics.DrawText(structValue.Name, new System.Numerics.Vector2(origin.X + 4, origin.Y - (lines * 15)), structValue.Color, 10, "FrizQuadrataITC:15", FontAlign.Left);
                        Graphics.DrawImage("Direction-Arrow.png", rectDirection, rectUV, structValue.Color);
                    }
                }

                // entities
                foreach (var entity in GameController.EntityListWrapper.Entities
                    .Where(x => x.Type == EntityType.Chest ||
                                x.Type == EntityType.Monster ||
                                x.Type == EntityType.IngameIcon ||
                                x.Type == EntityType.MiscellaneousObjects))
                {
                    var match = false;
                    var lineColor = Color.White;
                    var lineText = "";
                    if (entity.HasComponent<Chest>() && entity.IsOpened) continue;
                    if (entity.HasComponent<Monster>() && (!entity.IsAlive || !entity.IsValid)) continue;
                    if (entity.GetHudComponent<SoundStatus>() != null && !entity.IsValid)
                        entity.GetHudComponent<SoundStatus>().Invalid();
                    if (entity.Type == EntityType.IngameIcon &&
                        (!entity.IsValid || (entity?.GetComponent<MinimapIcon>().IsHide ?? true))) continue;
                    var delta = entity.GridPos - GameController.Player.GridPos;
                    var distance = delta.GetPolarCoordinates(out var phi);

                    var rectDirection = new RectangleF(origin.X - margin - height / 2,
                        origin.Y - margin / 2 - height - lines * height, height, height);
                    var rectUV = Get64DirectionsUV(phi, distance, 3);
                    var ePath = entity.Path;
                    // prune paths where relevant
                    if (ePath.Contains("@")) ePath = ePath.Split('@')[0];
                    // Hud component check
                    var structValue = entity.GetHudComponent<ProximityAlert>();
                    if (structValue != null && !mods.Contains(structValue.Name))
                    {
                        mods += structValue.Name;
                        lines++;
                        Graphics.DrawText(structValue.Name,
                            new Vector2(origin.X + height / 2, origin.Y - lines * height), structValue.Color, 10,
                            Settings.Font.Value);
                        // Graphics.DrawText(structValue.Name, new System.Numerics.Vector2(origin.X + 4, origin.Y - (lines * 15)), structValue.Color, 10, "FrizQuadrataITC:15", FontAlign.Left);
                        Graphics.DrawImage("Direction-Arrow.png", rectDirection, rectUV, structValue.Color);
                        match = true;
                    }

                    // Contains Check
                    if (!match)
                        foreach (var filterEntry in _pathDict.Where(x => ePath.Contains(x.Key)).Take(1))
                        {
                            var filter = filterEntry.Value;
                            unopened = $"{filter.Text}\n{unopened}";
                            if (filter.Distance == -1 || filter.Distance == -2 && entity.IsValid ||
                                distance < filter.Distance)
                            {
                                var soundStatus = entity.GetHudComponent<SoundStatus>() ?? null;
                                if (soundStatus == null || !soundStatus.PlayedCheck())
                                    entity.SetHudComponent(new SoundStatus(entity, filter.SoundFile));
                                lineText = filter.Text;
                                lineColor = filter.Color;
                                match = true;
                                lines++;
                                break;
                            }
                        }

                    // Hardcoded Chests
                    if (!match)
                        if (entity.HasComponent<Chest>() && ePath.Contains("Delve"))
                        {
                            var chestName = Regex.Replace(Path.GetFileName(ePath),
                                    @"((?<=\p{Ll})\p{Lu})|((?!\A)\p{Lu}(?>\p{Ll}))", " $0")
                                .Replace("Delve Chest ", string.Empty)
                                .Replace("Delve Azurite ", "Azurite ")
                                .Replace("Delve Mining Supplies ", string.Empty)
                                .Replace("_", string.Empty);
                            if (chestName.EndsWith(" Encounter") || chestName.EndsWith(" No Drops")) continue;
                            if (distance > 100)
                                if (chestName.Contains("Generic")
                                    || chestName.Contains("Vein")
                                    || chestName.Contains("Flare")
                                    || chestName.Contains("Dynamite")
                                    || chestName.Contains("Armour")
                                    || chestName.Contains("Weapon"))
                                    if (chestName.Contains("Path ") || !chestName.Contains("Currency"))
                                        continue;
                            if (chestName.Contains("Currency") || chestName.Contains("Fossil"))
                                lineColor = new Color(255, 0, 255);
                            if (chestName.Contains("Flares")) lineColor = new Color(0, 200, 255);
                            if (chestName.Contains("Dynamite") || chestName.Contains("Explosives"))
                                lineColor = new Color(255, 50, 50);
                            lineText = chestName;
                            lines++;
                            match = true;
                        }

                    if (match)
                    {
                        Graphics.DrawText(lineText, new Vector2(origin.X + height / 2, origin.Y - lines * height),
                            lineColor, 10, Settings.Font.Value);
                        // Graphics.DrawText(lineText, new System.Numerics.Vector2(origin.X + 4, origin.Y - (lines * 15)), lineColor, 10, "FrizQuadrataITC:15", FontAlign.Left);
                        Graphics.DrawImage("Direction-Arrow.png", rectDirection, rectUV, lineColor);
                    }
                }

                if (lines > 0)
                {
                    var widthMultiplier = 1 + height / 100;

                    var box = new RectangleF(origin.X - 2, origin.Y - margin - lines * height,
                        (192 + 4) * widthMultiplier, margin + lines * height + 4);
                    Graphics.DrawImage("back.png", box, Color.White);
                    Graphics.DrawLine(new SharpDX.Vector2(origin.X - 15, origin.Y - margin - lines * height),
                        new SharpDX.Vector2(origin.X + (192 + 4) * widthMultiplier,
                            origin.Y - margin - lines * height), 1, Color.White);
                    Graphics.DrawLine(new SharpDX.Vector2(origin.X - 15, origin.Y + 3),
                        new SharpDX.Vector2(origin.X + (192 + 4) * widthMultiplier, origin.Y + 3), 1, Color.White);
                }
            }
            catch
            {
                // ignored
            }
        }


        private class Warning
        {
            public string Text { get; set; }
            public Color Color { get; set; }
            public int Distance { get; set; }
            public string SoundFile { get; set; }
        }

        private class SoundStatus
        {
            public SoundStatus(Entity entity, string sound)
            {
                this.Entity = entity;
                if (!Played && entity.IsValid)
                {
                    lock (Locker)
                    {
                        PlaySound(sound);
                    }

                    Played = true;
                }
            }

            private Entity Entity { get; }
            private bool Played { get; set; }

            public void Invalid()
            {
                if (Played && !Entity.IsValid) Played = false;
            }

            public bool PlayedCheck()
            {
                return Played;
            }
        }

        private class ProximityAlert
        {
            public ProximityAlert(Entity entity, Color color)
            {
                Entity = entity;
                Color = color;
                Name = string.Empty;
                PlayWarning = true;
            }

            private Entity Entity { get; }
            public string Name { get; private set; }
            public Color Color { get; private set; }
            private bool PlayWarning { get; set; }

            public void Update()
            {
                if (!Entity.IsValid) PlayWarning = true;
                if (!Entity.HasComponent<ObjectMagicProperties>() || !Entity.IsAlive) return;
                var mods = Entity.GetComponent<ObjectMagicProperties>()?.Mods;
                if (mods == null || mods.Count <= 0) return;
                var modMatch = mods.FirstOrDefault(x => _modDict.ContainsKey(x));
                var filter = _modDict[modMatch ?? string.Empty];
                if (filter == null) return;
                Name = filter.Text;
                Color = filter.Color;
                if (PlayWarning)
                    lock (Locker)
                    {
                        PlaySound(filter.SoundFile);
                    }

                PlayWarning = false;
            }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class StaticEntity
        {
            public StaticEntity(string path, Vector3 pos)
            {
                Path = path;
                Pos = pos;
            }

            public string Path { get; }
            public Vector3 Pos { get; }
        }
    }
}