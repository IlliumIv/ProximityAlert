using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExileCore.RenderQ;
using ExileCore.Shared.Nodes;
using ImGuiNET;

namespace ProximityAlert
{
    partial class Proximity
    {
        private Dictionary<string, FontContainer> Fonts { get; } = new Dictionary<string, FontContainer>();

        private static int IntSlider(string labelString, RangeNode<int> setting)
        {
            var refValue = setting.Value;
            ImGui.SliderInt(labelString, ref refValue, setting.Min, setting.Max);
            return refValue;
        }

        private static float FloatSlider(string labelString, RangeNode<float> setting)
        {
            var refValue = setting.Value;
            ImGui.SliderFloat(labelString, ref refValue, setting.Min, setting.Max);
            return refValue;
        }

        private static bool Checkbox(string labelString, bool boolValue)
        {
            ImGui.Checkbox(labelString, ref boolValue);
            return boolValue;
        }

        private static void HelpMarker(string desc)
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

        private static string ComboBox(string labelString, ListNode setting)
        {
            var items = setting.Values.ToArray();
            var refValue = Array.IndexOf(items, setting.Value);
            ImGui.Combo(labelString, ref refValue, items, items.Length);
            return items[refValue];
        }

        private unsafe void SetFonts()
        {
            const string folder = "fonts";
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
            var some = &sm;

            var imFontAtlasGetGlyphRangesCyrillic = ImGuiNative.ImFontAtlas_GetGlyphRangesCyrillic(some);
            Fonts["Default:13"] = new FontContainer(ImGuiNative.ImFontAtlas_AddFontDefault(some, null),
                "Default", 13);

            foreach (var (item1, item2) in fontsForLoad)
            {
                var bytes = Encoding.UTF8.GetBytes(item1);

                fixed (byte* f = &bytes[0])
                {
                    Fonts[$"{item1.Replace(".ttf", "").Replace("fonts\\", "")}:{item2}"] =
                        new FontContainer(
                            ImGuiNative.ImFontAtlas_AddFontFromFileTTF(some, f, item2, null,
                                imFontAtlasGetGlyphRangesCyrillic), item1, item2);
                }
            }

            Settings.Font.Values = new List<string>(Fonts.Keys);
        }

        public override void DrawSettings()
        {
            Settings.Font.Value = ComboBox("Fonts", Settings.Font);
            Settings.Scale.Value = FloatSlider("Height Scale", Settings.Scale);
            ImGui.SameLine(); HelpMarker("Height Scale.");
            Settings.ProximityX.Value = IntSlider("Proximity X Position", Settings.ProximityX);
            ImGui.SameLine(); HelpMarker("Relative to the center of the screen.");
            Settings.ProximityY.Value = IntSlider("Proximity Y Position", Settings.ProximityY);
            ImGui.SameLine(); HelpMarker("Relative to the center of the screen.");
            ImGui.SameLine();
            HelpMarker("Relative to the center of the screen.");
            Settings.MultiThreading.Value = Checkbox("Enable Multithreading", Settings.MultiThreading);
            Settings.ShowModAlerts.Value = Checkbox("Show Alerts for Modifiers", Settings.ShowModAlerts);
            ImGui.SameLine();
            HelpMarker("By default this covers things such as corrupting blood.");
            Settings.ShowPathAlerts.Value = Checkbox("Show Alerts for Paths", Settings.ShowPathAlerts);
            ImGui.SameLine();
            HelpMarker("By default this covers special monsters, chests, fossils, sulphite, delirium mirrors etc.");
            Settings.PlaySounds.Value = Checkbox("Play Sounds for Alerts", Settings.PlaySounds);
            ImGui.SameLine();
            HelpMarker("Sounds can be found and go in Hud\\Plugins\\Compiled\\ProximityAlert\\");
            if (ImGui.Button("Reload ModAlerts & PathAlerts.txt"))
            {
                LogMessage("Reloading ModAlerts & PathAlerts.txt...");
                _pathDict = LoadConfig(Path.Combine(DirectoryFullName, "PathAlerts.txt"));
                _modDict = LoadConfig(Path.Combine(DirectoryFullName, "ModAlerts.txt"));
            }

            Settings.ShowSirusLine.Value = Checkbox("Draw a Line to Real Sirus", Settings.ShowSirusLine);
        }
    }
}