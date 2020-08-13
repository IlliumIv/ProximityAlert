using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;

namespace Proximity
{
    public class ProximitySettings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        [Menu("Proximity X Position")] public RangeNode<int> ProximityX { get; set; } = new RangeNode<int>(0, -3840, 2560);
        [Menu("Proximity Y Position")] public RangeNode<int> ProximityY { get; set; } = new RangeNode<int>(0, -3840, 2560);
        [Menu("Enable Multi Threading")] public ToggleNode MultiThreading { get; set; } = new ToggleNode(true);
        [Menu("Enable Mob Mod Alerts")] public ToggleNode ShowModAlerts { get; set; } = new ToggleNode(true);
        [Menu("Render Nearby Monsters")] public ToggleNode ShowNearby { get; set; } = new ToggleNode(true);
        [Menu("Render Line to Sirus")] public ToggleNode ShowSirusLine { get; set; } = new ToggleNode(true);
        [Menu("Render Line to Sirus")] public ToggleNode PlaySounds { get; set; } = new ToggleNode(true);
        
        // Debug
        // [Menu("Add Nearby Monsters Key")] public HotkeyNode ShowNearbyKey { get; set; } = new HotkeyNode(Keys.Menu);

    }
}
