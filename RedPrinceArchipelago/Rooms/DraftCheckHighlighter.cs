using RedPrinceArchipelago.Archipelago;
using RedPrinceArchipelago.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace RedPrinceArchipelago.Rooms;

/// <summary>
/// Adds a visual-only electric-blue glow to room names in the draft interface
/// when choosing that room can complete an unchecked First Entering location.
/// This does not touch the game's electricity or room-power state.
/// </summary>
public static class DraftCheckHighlighter
{
    private sealed class OriginalOutline
    {
        public TextMeshPro Text;
        public float Width;
        public Color32 Color;
    }

    private static readonly Dictionary<int, OriginalOutline> Highlighted = [];
    private static readonly Color32 CheckGlowColor = new(35, 180, 255, 255);
    private static float _nextRefreshTime;

    public static void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime) return;
        _nextRefreshTime = Time.unscaledTime + 0.2f;

        if (!ArchipelagoClient.Authenticated)
        {
            RestoreAll();
            return;
        }

        HashSet<int> seen = [];
        foreach (TextMeshPro text in Resources.FindObjectsOfTypeAll<TextMeshPro>())
        {
            if (text == null || !text.gameObject.activeInHierarchy || !IsDraftInterfaceText(text)) continue;

            ModRoom room = FindDisplayedRoom(text.text);
            if (room == null || !HasUncheckedFirstEntry(room)) continue;

            int instanceId = text.GetInstanceID();
            seen.Add(instanceId);
            if (!Highlighted.ContainsKey(instanceId))
            {
                Highlighted[instanceId] = new OriginalOutline
                {
                    Text = text,
                    Width = text.outlineWidth,
                    Color = text.outlineColor,
                };
            }

            text.outlineColor = CheckGlowColor;
            text.outlineWidth = Math.Max(text.outlineWidth, 0.35f);
        }

        foreach (int instanceId in Highlighted.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            Restore(instanceId);
        }
    }

    public static void RestoreAll()
    {
        foreach (int instanceId in Highlighted.Keys.ToArray())
        {
            Restore(instanceId);
        }
    }

    private static void Restore(int instanceId)
    {
        OriginalOutline original = Highlighted[instanceId];
        if (original.Text != null)
        {
            original.Text.outlineColor = original.Color;
            original.Text.outlineWidth = original.Width;
        }
        Highlighted.Remove(instanceId);
    }

    private static bool IsDraftInterfaceText(TextMeshPro text)
    {
        string path = text.gameObject.GetPath();
        return path.IndexOf("DRAFT", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("PLAN PICK", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("ROOM CHOICE", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static ModRoom FindDisplayedRoom(string displayedName)
    {
        if (string.IsNullOrWhiteSpace(displayedName)) return null;

        string normalized = RemoveRichText(displayedName).Trim();
        ModRoom room = Plugin.ModRoomManager.GetRoomByName(normalized);
        if (room != null) return room;

        return Plugin.ModRoomManager.Rooms.FirstOrDefault(candidate =>
            string.Equals(candidate.Name.ToTitleCase(), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.GameObjectName.ToTitleCase(), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string RemoveRichText(string value)
    {
        while (true)
        {
            int start = value.IndexOf('<');
            if (start < 0) return value;
            int end = value.IndexOf('>', start);
            if (end < 0) return value;
            value = value.Remove(start, end - start + 1);
        }
    }

    private static bool HasUncheckedFirstEntry(ModRoom room)
    {
        if (room.Name.StartsWith("CLASSROOM", StringComparison.OrdinalIgnoreCase))
        {
            return ArchipelagoClient.ServerData.LocationDict.Any(entry =>
                entry.Value.StartsWith("Classroom ", StringComparison.OrdinalIgnoreCase) &&
                entry.Value.EndsWith(" First Entering", StringComparison.OrdinalIgnoreCase) &&
                !ArchipelagoClient.ServerData.CheckedLocations.Contains(entry.Key));
        }

        string locationName = $"{room.Name.ToTitleCase()} First Entering";
        if (IsUncheckedLocation(locationName)) return true;

        return room.Name.Equals("BUNK ROOM", StringComparison.OrdinalIgnoreCase) &&
               IsUncheckedLocation("Bunk Room First Entering 2");
    }

    private static bool IsUncheckedLocation(string locationName)
    {
        long locationId = Plugin.ArchipelagoClient.GetLocationFromName(locationName);
        return locationId >= 0 && !ArchipelagoClient.ServerData.CheckedLocations.Contains(locationId);
    }
}
