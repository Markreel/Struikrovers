using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Struikrovers.Core
{
    public static class MapLoader
    {
        public const string MapsFolderName = "Maps";

        // Names in the JSON that differ from the enum names.
        private static readonly Dictionary<string, TerrainType> Aliases =
            new Dictionary<string, TerrainType>(StringComparer.OrdinalIgnoreCase)
            {
                { "solid", TerrainType.Tower },
            };

        private class MapDto
        {
            [JsonProperty("width")] public int Width;
            [JsonProperty("height")] public int Height;
            [JsonProperty("tiles")] public List<List<string>> Tiles;
        }

        public static bool TryLoadFromStreamingAssets(
            string fileName, out MapData map, out string error)
        {
            map = null;
            string safeName = Path.GetFileName(fileName ?? "");

            if (string.IsNullOrWhiteSpace(safeName) ||
                !safeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                error = "Choose a .json map from Assets/StreamingAssets/Maps in the Inspector.";
                return false;
            }

            string path = Path.Combine(
                Application.streamingAssetsPath, MapsFolderName, safeName);

            if (!File.Exists(path))
            {
                error = "Board map not found: " + path;
                return false;
            }

            return TryParse(File.ReadAllText(path), safeName, out map, out error);
        }

        public static bool TryParse(
            string json, string mapName, out MapData map, out string error)
        {
            map = null;
            MapDto dto;

            try
            {
                dto = JsonConvert.DeserializeObject<MapDto>(json);
            }
            catch (JsonException e)
            {
                error = $"Map '{mapName}' is not valid JSON: {e.Message}";
                return false;
            }

            if (dto?.Tiles == null || dto.Tiles.Count == 0)
            {
                error = $"No tiles found in map '{mapName}'.";
                return false;
            }

            int height = dto.Tiles.Count;
            int width = 0;
            foreach (List<string> row in dto.Tiles)
                width = Mathf.Max(width, row.Count);

            if ((dto.Width != 0 && dto.Width != width) ||
                (dto.Height != 0 && dto.Height != height))
            {
                Debug.LogWarning(
                    $"Map '{mapName}' declares {dto.Width}x{dto.Height} but contains " +
                    $"{width}x{height} tiles. Using the actual tile data.");
            }

            var tiles = new TerrainType[width, height];
            var unknown = new StringBuilder();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Short rows are padded with grass.
                    if (x >= dto.Tiles[y].Count)
                    {
                        tiles[x, y] = TerrainType.Grass;
                        continue;
                    }

                    string name = dto.Tiles[y][x];
                    if (TryParseTerrain(name, out TerrainType terrain))
                        tiles[x, y] = terrain;
                    else
                        unknown.AppendLine($"  ({x},{y}): '{name}'");
                }
            }

            if (unknown.Length > 0)
            {
                error = $"Map '{mapName}' contains unknown terrain names:\n{unknown}";
                return false;
            }

            map = new MapData(mapName, tiles);
            error = null;
            return true;
        }

        private static bool TryParseTerrain(string name, out TerrainType terrain)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (Aliases.TryGetValue(name.Trim(), out terrain))
                    return true;
                if (Enum.TryParse(name.Trim(), true, out terrain))
                    return true;
            }

            terrain = TerrainType.Grass;
            return false;
        }
    }
}