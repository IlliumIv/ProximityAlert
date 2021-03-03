using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExileCore.RenderQ;
using ExileCore.Shared.Nodes;
using ImGuiNET;
namespace Proximity
{
    partial class Proximity
    {
        public static int IntSlider(string labelString, RangeNode<int> setting)
        {
            var refValue = setting.Value;
            ImGui.SliderInt(labelString, ref refValue, setting.Min, setting.Max);
            return refValue;
        }

        public static float FloatSlider(string labelString, RangeNode<float> setting)
        {
            var refValue = setting.Value;
            ImGui.SliderFloat(labelString, ref refValue, setting.Min, setting.Max);
            return refValue;
        }

        public static bool Checkbox(string labelString, bool boolValue)
        {
            ImGui.Checkbox(labelString, ref boolValue);
            return boolValue;
        }
        public static void HelpMarker(string desc)
        {
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(desc);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

        public static string ComboBox(string labelString, ListNode setting)
        {
            var items = setting.Values.ToArray();
            var refValue = Array.IndexOf(items, setting.Value);
            ImGui.Combo(labelString, ref refValue, items, items.Length);
            return items[refValue];
        }

        public Dictionary<string, FontContainer> fonts { get; }  = new Dictionary<string, FontContainer>();

        private unsafe void SetFonts()
        {
            var folder = "fonts";
            var files = Directory.GetFiles(folder);

            if (!(Directory.Exists(folder) && files.Length > 0))
                return;

            var fontsForLoad = new List<(string, int)>();

            if (files.Contains($"{folder}\\config.ini"))
            {
                var lines = File.ReadAllLines($"{folder}\\config.ini");

                foreach (var line in lines)
                {
                    var split = line.Split(':');
                    fontsForLoad.Add(($"{folder}\\{split[0]}.ttf", int.Parse(split[1])));
                }
            }

            var sm = new ImFontAtlas();
            ImFontAtlas* some = &sm;

            var imFontAtlasGetGlyphRangesCyrillic = ImGuiNative.ImFontAtlas_GetGlyphRangesCyrillic(some);
            fonts["Default:13"] = new FontContainer(ImGuiNative.ImFontAtlas_AddFontDefault(some, null),
                "Default", 13);

            foreach (var tuple in fontsForLoad)
            {
                var bytes = Encoding.UTF8.GetBytes(tuple.Item1);

                fixed (byte* f = &bytes[0])
                {
                    fonts[$"{tuple.Item1.Replace(".ttf", "").Replace("fonts\\", "")}:{tuple.Item2}"] =
                        new FontContainer(
                            ImGuiNative.ImFontAtlas_AddFontFromFileTTF(some, f, tuple.Item2, null,
                                imFontAtlasGetGlyphRangesCyrillic), tuple.Item1, tuple.Item2);
                }
            }

            Settings.Font.Values = new List<string>(fonts.Keys);
        }

        public override void DrawSettings()
        {
            Settings.Font.Value = ComboBox("Fonts", Settings.Font);
            Settings.Scale.Value = FloatSlider("Height Scale", Settings.Scale);
            ImGui.SameLine(); HelpMarker("Height Scale.");
            Settings.ProximityX.Value = IntSlider("Proximity X Position##proxx", Settings.ProximityX);
            ImGui.SameLine(); HelpMarker("Relative to the center of the screen.");
            Settings.ProximityY.Value = IntSlider("Proximity Y Position##proxy", Settings.ProximityY);
            ImGui.SameLine(); HelpMarker("Relative to the center of the screen.");
            Settings.MultiThreading.Value = Checkbox("Enable Multithreading", Settings.MultiThreading);
            Settings.ShowModAlerts.Value = Checkbox("Show Alerts for Modifiers", Settings.ShowModAlerts);
            ImGui.SameLine(); HelpMarker("By default this covers things such as corrupting blood.");
            Settings.ShowNearby.Value = Checkbox("Show Alerts for Paths", Settings.ShowNearby);
            ImGui.SameLine(); HelpMarker("By default this covers special monsters, chests, fossils, sulphite, delirium mirrors etc.");
            Settings.PlaySounds.Value = Checkbox("Play Sounds for Alerts", Settings.PlaySounds);
            ImGui.SameLine(); HelpMarker($"Sounds can be found and go in Hud\\Plugins\\Compiled\\ProximityAlert\\");
            if (ImGui.Button("Reload ModAlerts & PathAlerts.txt"))
            {
                LogMessage("Reloading ModAlerts & PathAlerts.txt...");
                PathDict = LoadConfig(Path.Combine(DirectoryFullName, "PathAlerts.txt"));
                ModDict = LoadConfig(Path.Combine(DirectoryFullName, "ModAlerts.txt"));
            }
            Settings.ShowSirusLine.Value = Checkbox("Draw a Line to Real Sirus", Settings.ShowSirusLine);
        }
    }
}
