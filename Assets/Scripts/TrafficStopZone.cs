using UnityEngine;

public class TrafficStopZone : MonoBehaviour
{
    public TrafficLightController controller;
    public bool northSouth;
    public bool IsRed => controller != null && (northSouth ? controller.NorthSouthRed : controller.EastWestRed);
}
