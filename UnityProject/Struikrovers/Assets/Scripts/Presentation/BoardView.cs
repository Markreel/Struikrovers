using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// Builds the board (map tiles, castles, frame) from prefabs and draws the
    /// current game state onto it. It never decides rules.
    public sealed class BoardView : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private RectTransform boardRoot;
        [SerializeField] private float cellSpacing = 2f;

        [Header("Prefabs")]
        [SerializeField] private TileView tilePrefab;
        [SerializeField] private CastleView castlePrefab;
        [SerializeField] private FrameCellView frameCellPrefab;
        [SerializeField] private PawnBadgeView badgePrefab;

        [Header("Style (optional, defaults are used when empty)")]
        [SerializeField] private VisualTheme theme;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly Dictionary<Vector2Int, TileView> tiles = new Dictionary<Vector2Int, TileView>();
        private readonly Dictionary<Vector2Int, CastleView> castles = new Dictionary<Vector2Int, CastleView>();
        private readonly List<FrameCellView> frameCells = new List<FrameCellView>();

        /// A tile or castle was clicked. Positions outside the map are castles.
        public event Action<Vector2Int> PositionClicked;

        public VisualTheme Theme
        {
            get
            {
                if (theme == null)
                {
                    Debug.LogWarning("BoardView has no VisualTheme assigned. Using defaults.");
                    theme = ScriptableObject.CreateInstance<VisualTheme>();
                }

                return theme;
            }
        }

        public void Build(BoardGrid grid)
        {
            Clear();

            if (boardRoot == null)
                boardRoot = GetComponent<RectTransform>();

            if (tilePrefab == null || castlePrefab == null ||
                frameCellPrefab == null || badgePrefab == null)
            {
                Debug.LogError("BoardView: assign all four prefabs in the Inspector.");
                return;
            }

            GridLayoutGroup layout = boardRoot.GetComponent<GridLayoutGroup>();
            if (layout == null)
                layout = boardRoot.gameObject.AddComponent<GridLayoutGroup>();

            // The two extra outer rows and columns are a visual frame. The map is
            // centred inside it. The castle cells in that frame are real
            // positions one step outside the map (BoardGrid.GetCastlePosition).
            int visualColumns = grid.Columns + 2;
            int visualRows = grid.Rows + 2;

            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = visualColumns;
            layout.spacing = new Vector2(cellSpacing, cellSpacing);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.cellSize = new Vector2(
                (boardRoot.rect.width - cellSpacing * (visualColumns - 1)) / visualColumns,
                (boardRoot.rect.height - cellSpacing * (visualRows - 1)) / visualRows);

            for (int visualY = 0; visualY < visualRows; visualY++)
            {
                for (int visualX = 0; visualX < visualColumns; visualX++)
                {
                    var position = new Vector2Int(visualX - 1, visualY - 1);

                    if (grid.InBounds(position))
                    {
                        CreateTile(position);
                        continue;
                    }

                    int castleTeam = FindCastleTeam(grid, position);
                    if (castleTeam >= 0)
                        CreateCastle(castleTeam, position);
                    else
                        CreateFrameCell(visualX, visualY, visualColumns, visualRows);
                }
            }
        }

        public void Clear()
        {
            foreach (GameObject obj in spawned)
            {
                if (obj == null)
                    continue;

                // Destroy() is deferred; deactivating keeps the layout group from counting it.
                obj.SetActive(false);
                Destroy(obj);
            }

            spawned.Clear();
            tiles.Clear();
            castles.Clear();
            frameCells.Clear();
        }

        /// Draws everything. viewerTeam is whose turn it is; with hideEnemyBandits
        /// on, other teams' Bandits and the captives they carry are not drawn
        /// (unless the Bandit is exposed).
        public void Render(GameState state, ITurnState step, int viewerTeam, bool hideEnemyBandits)
        {
            if (tiles.Count == 0)
                return;

            BoardGrid grid = state.Grid;
            Dictionary<Vector2Int, List<PawnMarker>> markers =
                BuildMarkers(state, viewerTeam, hideEnemyBandits);

            HashSet<Vector2Int> destinations = ToSet(step?.Destinations);
            HashSet<Vector2Int> moveFightTargets = ToSet(step?.MoveFightTargets);
            HashSet<Vector2Int> blockedTargets = ToSet(step?.BlockedDestinations);
            HashSet<Vector2Int> abductionTargets = ToSet(step?.AbductionTargets);
            HashSet<Vector2Int> attackTargets = ToSet(step?.AttackTargets);
            HashSet<Vector2Int> actionTargets = ToSet(step?.ActionTargets);
            HashSet<Vector2Int> quickTargets = ToSet(step?.QuickActionTargets);
            Color actionColor = Theme.GetActionColor(step?.SelectedActionGroup);

            foreach (KeyValuePair<Vector2Int, TileView> entry in tiles)
            {
                Vector2Int position = entry.Key;
                markers.TryGetValue(position, out List<PawnMarker> list);

                TileHighlight highlight = TileHighlight.None;
                if (destinations.Contains(position)) highlight = TileHighlight.Destination;
                else if (moveFightTargets.Contains(position)) highlight = TileHighlight.MoveFightTarget;
                else if (blockedTargets.Contains(position)) highlight = TileHighlight.BlockedDestination;
                else if (abductionTargets.Contains(position)) highlight = TileHighlight.AbductionTarget;
                else if (attackTargets.Contains(position)) highlight = TileHighlight.AttackTarget;
                else if (actionTargets.Contains(position)) highlight = TileHighlight.ActionTarget;
                else if (quickTargets.Contains(position)) highlight = TileHighlight.QuickActionTarget;

                entry.Value.Show(grid.GetTerrain(position), highlight, actionColor, list);
            }

            // A castle lights up when it is a legal destination (carrying Bandit only).
            foreach (KeyValuePair<Vector2Int, CastleView> entry in castles)
                entry.Value.SetHighlighted(destinations.Contains(entry.Key));

            foreach (FrameCellView cell in frameCells)
                cell.Refresh(state);
        }

        // ---- Building ----

        private void CreateTile(Vector2Int position)
        {
            TileView tile = Instantiate(tilePrefab, boardRoot);
            tile.Initialize(position, Theme, badgePrefab);
            tile.Clicked += OnPositionClicked;

            tiles[position] = tile;
            spawned.Add(tile.gameObject);
        }

        private void CreateCastle(int team, Vector2Int position)
        {
            CastleView castle = Instantiate(castlePrefab, boardRoot);
            castle.Initialize(team, position, Theme);
            castle.Clicked += OnPositionClicked;

            castles[position] = castle;
            spawned.Add(castle.gameObject);
        }

        private void CreateFrameCell(int x, int y, int visualColumns, int visualRows)
        {
            int team = FrameLayout.GetEdgeTeam(x, y, visualColumns, visualRows);
            FrameStat stat = FrameLayout.GetStat(x, y, visualColumns, visualRows, out bool compact);

            FrameCellView cell = Instantiate(frameCellPrefab, boardRoot);
            cell.Configure(team, stat, compact, Theme);

            frameCells.Add(cell);
            spawned.Add(cell.gameObject);
        }

        private static int FindCastleTeam(BoardGrid grid, Vector2Int position)
        {
            for (int team = 0; team < GameConfig.TeamCount; team++)
                if (grid.GetCastlePosition(team) == position)
                    return team;

            return -1;
        }

        private void OnPositionClicked(Vector2Int position) => PositionClicked?.Invoke(position);

        // ---- Rendering helpers ----

        // A Bandit is visible to its own team, to everyone when hiding is off, and to
        // everyone while it is exposed (its tree was chopped).
        private static bool CanSee(Pawn bandit, int viewerTeam, bool hideEnemyBandits) =>
            !hideEnemyBandits || bandit.Team == viewerTeam || bandit.IsExposed;

        // Captives are drawn on their captor's tile as Royalty tokens. A hidden
        // Bandit hides its captives too, since they would give it away.
        private static Dictionary<Vector2Int, List<PawnMarker>> BuildMarkers(
            GameState state, int viewerTeam, bool hideEnemyBandits)
        {
            var lookup = new Dictionary<Vector2Int, List<PawnMarker>>();

            void Add(Vector2Int position, PawnMarker marker)
            {
                if (!lookup.TryGetValue(position, out List<PawnMarker> list))
                {
                    list = new List<PawnMarker>();
                    lookup[position] = list;
                }

                list.Add(marker);
            }

            foreach (Pawn pawn in state.Pawns)
            {
                if (pawn.AtCastle)
                    continue;

                if (pawn.Type == PawnType.Bandit && !CanSee(pawn, viewerTeam, hideEnemyBandits))
                    continue;

                Add(pawn.Position, new PawnMarker(pawn.Team, pawn.Type, false));
            }

            foreach (Pawn bandit in state.Pawns)
            {
                if (bandit.Type != PawnType.Bandit || bandit.AtCastle)
                    continue;

                if (!CanSee(bandit, viewerTeam, hideEnemyBandits))
                    continue;

                foreach (Pawn captive in bandit.Captives)
                    Add(bandit.Position, new PawnMarker(captive.Team, PawnType.Royalty, true));
            }

            return lookup;
        }

        private static HashSet<Vector2Int> ToSet(IReadOnlyCollection<Vector2Int> source) =>
            source == null ? new HashSet<Vector2Int>() : new HashSet<Vector2Int>(source);
    }
}