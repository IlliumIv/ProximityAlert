using System.Collections.Generic;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace ProximityAlert
{
    public class ProximitySettings : ISettings
    {
        public ListNode Font { get; set; } = new ListNode {Values = new List<string> {"FrizQuadrataITC:22"}};
        public RangeNode<float> Scale { get; set; } = new RangeNode<float>(1, (float) 0.1, 10);
        public RangeNode<int> ProximityX { get; set; } = new RangeNode<int>(0, -3840, 2560);
        public RangeNode<int> ProximityY { get; set; } = new RangeNode<int>(0, -3840, 2560);
        public ToggleNode MultiThreading { get; set; } = new ToggleNode(true);
        public ToggleNode ShowModAlerts { get; set; } = new ToggleNode(false);
        public ToggleNode ShowPathAlerts { get; set; } = new ToggleNode(true);
        public ToggleNode ShowSirusLine { get; set; } = new ToggleNode(true);
        public ToggleNode PlaySounds { get; set; } = new ToggleNode(true);
        public ToggleNode Enable { get; set; } = new ToggleNode(true);
    }
}