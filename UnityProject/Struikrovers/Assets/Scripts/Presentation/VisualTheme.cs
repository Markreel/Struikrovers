using System.Collections.Generic;
using UnityEngine;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// Every colour, symbol and sprite in one asset. Create one with
    /// Create > Struikrovers > Visual Theme. The defaults match the old look.
    [CreateAssetMenu(menuName = "Struikrovers/Visual Theme", fileName = "VisualTheme")]
    public sealed class VisualTheme : ScriptableObject
    {
        [Header("Teams (index = team number - 1)")]
        [SerializeField]
        private Color[] teamColors =
        {
            new Color(0.85f, 0.22f, 0.20f),
            new Color(0.20f, 0.45f, 0.90f),
            new Color(0.95f, 0.75f, 0.15f),
            new Color(0.60f, 0.30f, 0.80f)
        };

        [Header("Terrain")]
        [SerializeField] private Color grass = new Color(0.43f, 0.68f, 0.34f);
        [SerializeField] private Color bush = new Color(0.12f, 0.40f, 0.18f);
        [SerializeField] private Color tree = new Color(0.25f, 0.48f, 0.25f);
        [SerializeField] private Color stump = new Color(0.55f, 0.35f, 0.18f);
        [SerializeField] private Color largeTree = new Color(0.12f, 0.32f, 0.16f);
        [SerializeField] private Color lootBush = new Color(0.12f, 0.40f, 0.18f);
        [SerializeField] private Color tower = new Color(0.65f, 0.65f, 0.70f);
        [SerializeField] private Color terrainLabel = new Color(0.12f, 0.14f, 0.12f, 0.9f);

        [Header("Glow (plentiful tiles)")]
        [SerializeField] private Color largeTreeGlow = new Color(0.35f, 1f, 0.45f);
        [SerializeField] private Color lootBushGlow = new Color(1f, 0.35f, 0.85f);

        [Header("Highlights")]
        [SerializeField] private Color destination = new Color(0.95f, 0.95f, 0.35f);
        [SerializeField] private Color abductionTarget = new Color(1f, 0.55f, 0.25f);
        [SerializeField] private Color attackTarget = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color gatherAction = new Color(0.75f, 0.95f, 0.60f);
        [SerializeField] private Color stabAction = new Color(0.95f, 0.35f, 0.35f);
        [SerializeField] private Color otherAction = new Color(0.55f, 0.75f, 0.95f);
        [SerializeField] private Color quickAction = new Color(0.90f, 0.30f, 0.60f);

        [Header("Frame")]
        [SerializeField] private Color frameFiller = new Color(0.12f, 0.12f, 0.14f);
        [SerializeField] private Color frameDark = new Color(0.08f, 0.09f, 0.12f);

        [Header("Pawn sprites (optional, falls back to Resources/StruikroversIcons)")]
        [SerializeField] private Sprite badge;
        [SerializeField] private Sprite royalty;
        [SerializeField] private Sprite squire;
        [SerializeField] private Sprite bandit;
        [SerializeField] private Sprite bloodhound;

        private readonly Dictionary<string, Sprite> fallbackSprites = new Dictionary<string, Sprite>();
        private readonly HashSet<string> missingSprites = new HashSet<string>();

        public Color DestinationColor => destination;
        public Color AbductionTargetColor => abductionTarget;
        public Color AttackTargetColor => attackTarget;
        public Color QuickActionColor => quickAction;
        public Color FrameFillerColor => frameFiller;
        public Color TerrainLabelColor => terrainLabel;

        // ---- Actions ----

        /// Highlight colour of an action group (the one selected on the HUD).
        public Color GetActionColor(string group)
        {
            switch (group)
            {
                case ActionGroups.Gather: return gatherAction;
                case ActionGroups.Stab: return stabAction;
                default: return otherAction;
            }
        }

        // ---- Teams ----

        public Color GetTeamColor(int team) => teamColors[Mathf.Abs(team) % teamColors.Length];

        public Color GetCastleColor(int team) => Color.Lerp(GetTeamColor(team), Color.black, 0.25f);

        public Color GetFrameColor(int team) => Color.Lerp(GetTeamColor(team), frameDark, 0.78f);

        public Color GetStatusBackground(int team) => Color.Lerp(GetTeamColor(team), frameDark, 0.48f);

        // ---- Terrain ----

        public Color GetTerrainColor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Grass: return grass;
                case TerrainType.Bush: return bush;
                case TerrainType.Tree: return tree;
                case TerrainType.Stump: return stump;
                case TerrainType.LargeTree: return largeTree;
                case TerrainType.LootBush: return lootBush;
                case TerrainType.Tower: return tower;
                default: return Color.magenta;
            }
        }

        public string GetTerrainSymbol(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Grass: return "";
                case TerrainType.Bush: return "B";
                case TerrainType.Tree: return "T";
                case TerrainType.Stump: return "S";
                case TerrainType.LargeTree: return "LT";
                case TerrainType.LootBush: return "LB";
                case TerrainType.Tower: return "TOWER";
                default: return "?";
            }
        }

        public bool TryGetGlowColor(TerrainType terrain, out Color color)
        {
            switch (terrain)
            {
                case TerrainType.LargeTree: color = largeTreeGlow; return true;
                case TerrainType.LootBush: color = lootBushGlow; return true;
                default: color = Color.clear; return false;
            }
        }

        // ---- Pawns ----

        public Sprite BadgeSprite => Resolve(badge, "badge");

        public Sprite GetPawnSprite(PawnType type)
        {
            switch (type)
            {
                case PawnType.Royalty: return Resolve(royalty, "royalty");
                case PawnType.Squire: return Resolve(squire, "squire");
                case PawnType.Bandit: return Resolve(bandit, "bandit");
                case PawnType.Bloodhound: return Resolve(bloodhound, "bloodhound");
                default: return null;
            }
        }

        public string GetPawnInitial(PawnType type)
        {
            switch (type)
            {
                case PawnType.Royalty: return "R";
                case PawnType.Squire: return "S";
                case PawnType.Bandit: return "B";
                case PawnType.Bloodhound: return "H";
                default: return "?";
            }
        }

        private Sprite Resolve(Sprite assigned, string resourceName)
        {
            if (assigned != null)
                return assigned;

            if (fallbackSprites.TryGetValue(resourceName, out Sprite cached) && cached != null)
                return cached;

            if (missingSprites.Contains(resourceName))
                return null;

            Texture2D texture = Resources.Load<Texture2D>("StruikroversIcons/" + resourceName);
            if (texture == null)
            {
                missingSprites.Add(resourceName);
                return null;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);

            fallbackSprites[resourceName] = sprite;
            return sprite;
        }
    }
}