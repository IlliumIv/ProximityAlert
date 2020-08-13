using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using nuVector4 = System.Numerics.Vector4;
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

        public override void DrawSettings()
        {
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
            Settings.ShowSirusLine.Value = Checkbox("Draw a Line to Real Sirus", Settings.ShowModAlerts);
        }
    }
}
