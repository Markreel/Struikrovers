
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class StruikroversBoard : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private RectTransform boardRoot;
    [SerializeField] private float cellSpacing = 2f;
    [Tooltip("Select a JSON map from Assets/StreamingAssets/Maps using the Inspector dropdown.")]
    [SerializeField] private string mapFileName = "default-map.json";

    private class BoardMap
    {
        public List<List<string>> tiles = new List<List<string>>();
    }

    private class Pawn
    {
        public int team;
        public string type;
        public Vector2Int position;
        // Pawns in a castle are off-board. Their position stores their team's
        // entry tile, but they are not rendered there until they leave.
        public bool atCastle;
    }

    private class Captive
    {
        public int royaltyTeam;
        public int banditTeam;
    }

    private readonly List<GameObject> spawnedTiles = new();
    private readonly List<Pawn> pawns = new();
    private readonly List<Captive> captives = new();
    // Public sniffing clue shown to all players.
    private string sniffingClue = "";
    // The distance recorded at the previous sniff for each Bloodhound.
    private readonly Dictionary<Pawn, int> previousSniffDistances = new();
    private readonly List<Button> tileButtons = new();
    private readonly List<Image> tileImages = new();
    private readonly List<Vector2Int> tilePositions = new();
    private readonly List<Text> teamStatLabels = new();
    private readonly Dictionary<string, Sprite> pawnIconSprites = new();
    private readonly Dictionary<string, Texture2D> loadedPawnIconTextures = new();
    private Image statusBackground;

    private readonly int[] movementDie = { 3, 4, 4, 5, 5, 6 };

    private int rows;
    private int columns;
    private int currentTeam;
    private int currentPawnIndex;
    private int rolledMovement;
    private int bloodhoundMovesRemaining;
    private bool hasRolled;
    private bool waitingForDestination;
    private bool waitingForAction;

    private bool choosingAbductionTarget;
    private bool choosingPostAbductionDestination;

    private Pawn selectedRoyaltyForAbduction;
    private int stepsToSelectedRoyalty;
    private int remainingBanditMovement;

    private Button cancelSelectionButton;

    private readonly int[] teamWood = new int[4];
    private int[] teamCards = new int[4];

    private readonly Dictionary<Vector2Int, string> terrainByPosition
        = new Dictionary<Vector2Int, string>();

    private Pawn activePawn;
    private readonly HashSet<Vector2Int> reachable = new();
    private readonly HashSet<Vector2Int> abductionTargets = new();
    private readonly HashSet<Vector2Int> bloodhoundAttackTargets = new();

    // Rescued Royalty get a movement-only escape before the interrupted
    // Bloodhound activation resumes.
    private readonly Queue<Pawn> pendingRescueRoyalties = new();
    private bool resolvingRescueTurns;
    private bool rescueRunOnly;
    private Pawn interruptedBloodhound;
    private int interruptedTeam;
    private int interruptedPawnIndex;

    private Text statusText;
    private Button rollButton;
    private Text rollButtonText;

    private readonly List<Image> glowImages = new();
    private readonly List<Color> glowColors = new();


    private static readonly string[] PawnOrder =
        { "Royalty", "Squire", "Bandit" };

    private static readonly Color[] TeamColors =
    {
        new Color(0.85f, 0.22f, 0.20f),
        new Color(0.20f, 0.45f, 0.90f),
        new Color(0.95f, 0.75f, 0.15f),
        new Color(0.60f, 0.30f, 0.80f)
    };

    private void Start()
    {
        if (boardRoot == null)
            boardRoot = GetComponent<RectTransform>();

        LoadAndDrawBoard();
    }

    private void Update()
    {
        float pulse = (Mathf.Sin(Time.time * 3f) + 1f) / 2f;

        for (int i = 0; i < glowImages.Count; i++)
        {
            if (glowImages[i] == null)
                continue;

            Color color = glowColors[i];
            color.a = Mathf.Lerp(0.35f, 1f, pulse);
            glowImages[i].color = color;
        }


        // Spacebar activates the same button as a mouse click.
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame &&
            rollButton != null &&
            rollButton.interactable)
        {
            rollButton.onClick.Invoke();
        }

    }

    [ContextMenu("Reload Board")]
    public void LoadAndDrawBoard()
    {
        string mapsFolder = Path.Combine(Application.streamingAssetsPath, "Maps");
        string safeFileName = Path.GetFileName(mapFileName ?? "");

        if (string.IsNullOrWhiteSpace(safeFileName) ||
            !safeFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("Choose a .json map from Assets/StreamingAssets/Maps in the Inspector.");
            return;
        }

        string path = Path.Combine(mapsFolder, safeFileName);
        if (!File.Exists(path))
        {
            Debug.LogError(
                "Board map not found: " + path +
                ". Put your map JSON files in Assets/StreamingAssets/Maps and select one in the Inspector."
            );
            return;
        }

        string json = File.ReadAllText(path);
        string mapName = "StreamingAssets/Maps/" + safeFileName;

        BoardMap map = ParseMap(json);

        if (map.tiles.Count == 0)
        {
            Debug.LogError("No tiles found in map: " + mapName);
            return;
        }

        rows = map.tiles.Count;
        columns = 0;

        foreach (var row in map.tiles)
            columns = Mathf.Max(columns, row.Count);

        if (boardRoot == null)
        {
            Debug.LogError("Assign Board Root in the Inspector.");
            return;
        }

        ClearBoard();
        DrawBoard(map);
        CreateControls();
        SpawnTeams();
        RefreshBoard();
        UpdateStatus();
        Debug.Log("Loaded board map: " + mapName);
    }

    private BoardMap ParseMap(string json)
    {
        BoardMap map = new BoardMap();

        Match match = Regex.Match(json, "\"tiles\"\\s*:\\s*\\[");

        if (!match.Success)
            return map;

        int start = json.IndexOf('[', match.Index);
        int depth = 0;
        int end = -1;

        for (int i = start; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']')
            {
                depth--;
                if (depth == 0)
                {
                    end = i;
                    break;
                }
            }
        }

        if (end < 0)
            return map;

        string content = json.Substring(start + 1, end - start - 1);
        MatchCollection rowMatches =
            Regex.Matches(content, "\\[([^\\[\\]]*)\\]");

        foreach (Match rowMatch in rowMatches)
        {
            List<string> row = new();

            MatchCollection values =
                Regex.Matches(rowMatch.Groups[1].Value, "\"([^\"]+)\"");

            foreach (Match value in values)
                row.Add(value.Groups[1].Value);

            if (row.Count > 0)
                map.tiles.Add(row);
        }

        return map;
    }

    private void ClearBoard()
    {
        foreach (GameObject obj in spawnedTiles)
            if (obj != null) Destroy(obj);

        spawnedTiles.Clear();
        tileButtons.Clear();
        tileImages.Clear();
        tilePositions.Clear();
        teamStatLabels.Clear();
        pawns.Clear();
        captives.Clear();
        previousSniffDistances.Clear();
        reachable.Clear();
        abductionTargets.Clear();
        bloodhoundAttackTargets.Clear();
        pendingRescueRoyalties.Clear();
        glowImages.Clear();
        glowColors.Clear();

        resolvingRescueTurns = false;
        rescueRunOnly = false;
        interruptedBloodhound = null;
        sniffingClue = "";
        activePawn = null;
        currentTeam = 0;
        currentPawnIndex = 0;
        hasRolled = false;
        waitingForDestination = false;
        waitingForAction = false;

        Array.Clear(teamWood, 0, teamWood.Length);
        Array.Clear(teamCards, 0, teamCards.Length);

        Transform oldControls = boardRoot.parent.Find("GameControls");
        if (oldControls != null)
            Destroy(oldControls.gameObject);
    }

    private void DrawBoard(BoardMap map)
    {
        GridLayoutGroup grid = boardRoot.GetComponent<GridLayoutGroup>();

        if (grid == null)
            grid = boardRoot.gameObject.AddComponent<GridLayoutGroup>();

        // The two extra outer rows/columns are a visual frame, not playable
        // terrain. The actual map remains unchanged and centred inside it.
        int visualColumns = columns + 2;
        int visualRows = rows + 2;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = visualColumns;
        grid.spacing = new Vector2(cellSpacing, cellSpacing);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.padding = new RectOffset(0, 0, 0, 0);

        float cellWidth =
            (boardRoot.rect.width - cellSpacing * (visualColumns - 1)) / visualColumns;
        float cellHeight =
            (boardRoot.rect.height - cellSpacing * (visualRows - 1)) / visualRows;
        grid.cellSize = new Vector2(cellWidth, cellHeight);

        terrainByPosition.Clear();

        for (int visualY = 0; visualY < visualRows; visualY++)
        {
            for (int visualX = 0; visualX < visualColumns; visualX++)
            {
                int mapX = visualX - 1;
                int mapY = visualY - 1;

                if (mapX >= 0 && mapX < columns && mapY >= 0 && mapY < rows &&
                    mapY < map.tiles.Count && mapX < map.tiles[mapY].Count)
                {
                    Vector2Int position = new Vector2Int(mapX, mapY);
                    string terrain = map.tiles[mapY][mapX];
                    terrainByPosition[position] = terrain;
                    CreateTile(terrain, mapX, mapY);
                    continue;
                }

                int castleTeam = GetCastleTeamAtFrameCell(visualX, visualY);
                if (castleTeam >= 0)
                {
                    CreateFrameCell("CASTLE\nP" + (castleTeam + 1), true, castleTeam);
                }
                else
                {
                    string stats = GetFrameStatsText(visualX, visualY, visualColumns, visualRows);
                    CreateFrameCell(stats, false, GetFrameTeamForCell(visualX, visualY, visualColumns, visualRows));
                }
            }
        }
    }

    private int GetCastleTeamAtFrameCell(int x, int y)
    {
        if (y == 0 && x == 1 + columns / 2) return 0;
        if (x == columns + 1 && y == 1 + rows / 2) return 1;
        if (y == rows + 1 && x == 1 + columns / 2) return 2;
        if (x == 0 && y == 1 + rows / 2) return 3;
        return -1;
    }

    private int GetFrameTeamForCell(int x, int y, int visualColumns, int visualRows)
    {
        if (y == 0) return 0;
        if (x == visualColumns - 1) return 1;
        if (y == visualRows - 1) return 2;
        if (x == 0) return 3;
        return -1;
    }

    private string GetFrameStatsText(int x, int y, int visualColumns, int visualRows)
    {
        int team = GetFrameTeamForCell(x, y, visualColumns, visualRows);
        if (team < 0) return "";

        // Spread each team's information across the available cells along its edge.
        if (y == 0 || y == visualRows - 1)
        {
            int localX = x;
            if (localX == 0) return "TEAM " + (team + 1);
            if (localX == 1) return "WOOD\n" + teamWood[team];
            if (localX == visualColumns - 2) return "CARDS\n" + teamCards[team];
            return "";
        }

        int localY = y;
        if (localY == 1) return "P" + (team + 1);
        if (localY == 2) return "W " + teamWood[team];
        if (localY == visualRows - 2) return "C " + teamCards[team];
        return "";
    }

    private void CreateFrameCell(string text, bool isCastle, int team)
    {
        GameObject cell = new GameObject(
            isCastle ? $"Castle_Team_{team + 1}" : "BoardFrame",
            typeof(RectTransform), typeof(Image)
        );
        cell.transform.SetParent(boardRoot, false);
        spawnedTiles.Add(cell);

        Image image = cell.GetComponent<Image>();
        if (team >= 0)
        {
            Color teamColor = TeamColors[team];
            image.color = isCastle
                ? Color.Lerp(teamColor, Color.black, 0.25f)
                : Color.Lerp(teamColor, new Color(0.08f, 0.09f, 0.12f), 0.78f);
        }
        else
        {
            image.color = new Color(0.12f, 0.12f, 0.14f);
        }

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(cell.transform, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2, 2);
        rect.offsetMax = new Vector2(-2, -2);

        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.MiddleCenter;
        label.fontStyle = FontStyle.Bold;
        label.fontSize = isCastle ? 11 : 10;
        label.color = Color.white;
        label.text = text;
        label.raycastTarget = false;

        if (!isCastle && team >= 0)
            teamStatLabels.Add(label);
    }

    private void CreateTile(string terrain, int x, int y)
    {
        GameObject tile = new GameObject(
            $"Tile_{x}_{y}_{terrain}",
            typeof(RectTransform), typeof(Image), typeof(Button)
        );

        tile.transform.SetParent(boardRoot, false);
        spawnedTiles.Add(tile);

        Image image = tile.GetComponent<Image>();
        image.color = GetTerrainColor(terrain);
        tileImages.Add(image);
        tilePositions.Add(new Vector2Int(x, y));
        CreateGlowOutline(tile, terrain);

        Button button = tile.GetComponent<Button>();
        Vector2Int position = new(x, y);
        button.onClick.AddListener(() => OnTileClicked(position));
        tileButtons.Add(button);

        GameObject labelObject = new GameObject(
            "Label", typeof(RectTransform), typeof(Text)
        );

        labelObject.transform.SetParent(tile.transform, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 14;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.black;
        label.raycastTarget = false;
        labelObject.transform.SetAsLastSibling();
    }

    private void CreateControls()
    {
        Canvas canvas = boardRoot.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("BoardRoot must be inside a Canvas.");
            return;
        }

        GameObject panel = new GameObject(
            "GameControls", typeof(RectTransform)
        );

        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.05f, 0.08f);
        panelRect.anchorMax = new Vector2(0.95f, 0.21f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // The background Image and the text must be separate UI objects.
        // Unity only permits one Graphic component (such as Image or Text) per GameObject.
        GameObject statusObject = new GameObject(
            "Status", typeof(RectTransform), typeof(Image)
        );
        statusObject.transform.SetParent(panel.transform, false);

        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0.45f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;

        statusBackground = statusObject.GetComponent<Image>();
        statusBackground.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);

        GameObject statusTextObject = new GameObject(
            "Label", typeof(RectTransform), typeof(Text)
        );
        statusTextObject.transform.SetParent(statusObject.transform, false);

        RectTransform statusTextRect = statusTextObject.GetComponent<RectTransform>();
        statusTextRect.anchorMin = Vector2.zero;
        statusTextRect.anchorMax = Vector2.one;
        statusTextRect.offsetMin = new Vector2(12f, 4f);
        statusTextRect.offsetMax = new Vector2(-12f, -4f);

        statusText = statusTextObject.GetComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 18;
        statusText.fontStyle = FontStyle.Bold;
        statusText.color = Color.white;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusText.verticalOverflow = VerticalWrapMode.Overflow;
        statusText.supportRichText = true;
        statusText.raycastTarget = false;

        GameObject buttonObject = new GameObject(
            "RollButton", typeof(RectTransform), typeof(Image), typeof(Button)
        );
        buttonObject.transform.SetParent(panel.transform, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.3f, -0.4f);
        buttonRect.anchorMax = new Vector2(0.7f, 0f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        buttonObject.GetComponent<Image>().color =
            new Color(0.25f, 0.65f, 0.3f);

        rollButton = buttonObject.GetComponent<Button>();
        rollButton.onClick.AddListener(RollOrFinish);

        GameObject buttonLabelObject = new GameObject(
            "Label", typeof(RectTransform), typeof(Text)
        );
        buttonLabelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform buttonLabelRect =
            buttonLabelObject.GetComponent<RectTransform>();
        buttonLabelRect.anchorMin = Vector2.zero;
        buttonLabelRect.anchorMax = Vector2.one;
        buttonLabelRect.offsetMin = Vector2.zero;
        buttonLabelRect.offsetMax = Vector2.zero;

        rollButtonText = buttonLabelObject.GetComponent<Text>();
        rollButtonText.font =
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rollButtonText.fontSize = 18;
        rollButtonText.fontStyle = FontStyle.Bold;
        rollButtonText.color = Color.white;
        rollButtonText.alignment = TextAnchor.MiddleCenter;

        // Cancel button for temporary abduction selection.
        GameObject cancelObject = new GameObject(
            "CancelSelectionButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );

        cancelObject.transform.SetParent(panel.transform, false);

        RectTransform cancelRect = cancelObject.GetComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0.05f, 0f);
        cancelRect.anchorMax = new Vector2(0.35f, 0.4f);
        cancelRect.offsetMin = Vector2.zero;
        cancelRect.offsetMax = Vector2.zero;

        cancelObject.GetComponent<Image>().color =
            new Color(0.65f, 0.25f, 0.25f);

        cancelSelectionButton = cancelObject.GetComponent<Button>();
        cancelSelectionButton.onClick.AddListener(CancelAbductionSelection);

        GameObject cancelLabelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(Text)
        );

        cancelLabelObject.transform.SetParent(cancelObject.transform, false);

        RectTransform cancelLabelRect =
            cancelLabelObject.GetComponent<RectTransform>();

        cancelLabelRect.anchorMin = Vector2.zero;
        cancelLabelRect.anchorMax = Vector2.one;
        cancelLabelRect.offsetMin = Vector2.zero;
        cancelLabelRect.offsetMax = Vector2.zero;

        Text cancelLabel = cancelLabelObject.GetComponent<Text>();
        cancelLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cancelLabel.fontSize = 16;
        cancelLabel.fontStyle = FontStyle.Bold;
        cancelLabel.color = Color.white;
        cancelLabel.alignment = TextAnchor.MiddleCenter;
        cancelLabel.text = "Cancel Selection";

        cancelObject.SetActive(false);
    }

    private void SpawnTeams()
    {
        // Team 0: top, 1: right, 2: bottom, 3: left.
        Vector2Int[] starts =
        {
            new(columns / 2, 0),
            new(columns - 1, rows / 2),
            new(columns / 2, rows - 1),
            new(0, rows / 2)
        };

        for (int team = 0; team < 4; team++)
        {
            foreach (string type in PawnOrder)
            {
                pawns.Add(new Pawn
                {
                    team = team,
                    type = type,
                    position = starts[team],
                    atCastle = true
                });
            }
        }

        currentTeam = 0;
        currentPawnIndex = 0;
        activePawn = GetActivePawn();
    }


    private Pawn GetActivePawn()
    {
        string requiredType = PawnOrder[currentPawnIndex];

        Pawn pawn = pawns.Find(p =>
            p.team == currentTeam &&
            p.type == requiredType);

        // An abducted Royalty becomes a Bloodhound,
        // but still occupies the Royalty turn slot.
        if (pawn == null && requiredType == "Royalty")
        {
            pawn = pawns.Find(p =>
                p.team == currentTeam &&
                p.type == "Bloodhound");
        }

        return pawn;
    }



    private void OnTileClicked(Vector2Int position)
    {
        if (activePawn == null)
            return;

        // During the action phase, clicking an adjacent tree
        // lets the Squire chop it.
        if (waitingForAction)
        {
            // Bloodhound attacks a hiding place. Every enemy Bandit there
            // releases all of its captives; the Bloodhound's own team's
            // Bandits and captives are unaffected.
            if (activePawn.type == "Bloodhound")
            {
                if (!bloodhoundAttackTargets.Contains(position))
                {
                    sniffingClue = "Choose a highlighted bush or tree to attack.";
                    UpdateStatus();
                    return;
                }

                ResolveBloodhoundAttack(position);
                return;
            }

            // Royalty can loot an adjacent bush.
            if (activePawn.type == "Royalty" && IsAdjacent(activePawn.position, position))
            {
                string terrain = GetTerrainAt(position).ToLowerInvariant();

                if (terrain == "bush" || terrain == "lootbush")
                {
                    // A Bandit hiding in this bush abducts the Royalty instead
                    // of allowing it to collect cards. Choose randomly for now.
                    List<Pawn> hiddenBandits = pawns.FindAll(p =>
                        p.type == "Bandit" &&
                        p.position == position &&
                        p.team != activePawn.team &&
                        !HasCaptivesForBandit(p.team));

                    if (hiddenBandits.Count > 0)
                    {
                        Pawn chosenBandit = hiddenBandits[
                            UnityEngine.Random.Range(0, hiddenBandits.Count)];

                        AbductRoyalty(activePawn, chosenBandit);
                        FinishActivation();
                        return;
                    }

                    LootBush(position);
                    return;
                }
            }

            // Bandit can abduct an adjacent enemy Royalty.
            if (activePawn.type == "Bandit" &&
                IsAdjacent(activePawn.position, position))
            {
                Pawn royalty = GetRoyaltyAt(position);

                if (royalty != null && royalty.team != currentTeam)
                {
                    AbductRoyalty(royalty);
                    FinishActivation();
                    return;
                }
            }

            // Squire can chop trees
            if (activePawn.type == "Squire" &&
                IsAdjacent(activePawn.position, position) &&
                IsTree(position))
            {
                ChopTree(position);
            }

            return;
        }


        // During Bandit movement, selecting an enemy Royalty
        // previews an abduction without committing it.
        if (waitingForDestination &&
            activePawn.type == "Bandit" &&
            abductionTargets.Contains(position))
        {
            selectedRoyaltyForAbduction = GetRoyaltyAt(position);

            if (selectedRoyaltyForAbduction == null)
                return;

            Dictionary<Vector2Int, int> distances =
                GetBanditDistances(
                    activePawn.position,
                    rolledMovement
                );

            stepsToSelectedRoyalty =
                distances[selectedRoyaltyForAbduction.position];

            remainingBanditMovement =
                rolledMovement - stepsToSelectedRoyalty;

            choosingAbductionTarget = false;
            choosingPostAbductionDestination = true;

            // Show only destinations available after the abduction.
            reachable.Clear();

            HashSet<Vector2Int> postAbductionDestinations =
                GetBanditDestinations(
                    selectedRoyaltyForAbduction.position,
                    remainingBanditMovement
                );

            foreach (Vector2Int destination in postAbductionDestinations)
                reachable.Add(destination);

            if (cancelSelectionButton != null)
                cancelSelectionButton.gameObject.SetActive(true);

            RefreshBoard();
            UpdateStatus();
            return;
        }


        // Commit the abduction by choosing a final destination.
        if (waitingForDestination &&
            choosingPostAbductionDestination &&
            reachable.Contains(position))
        {
            if (selectedRoyaltyForAbduction == null)
                return;

            // Move both pieces to the chosen bush/tree.
            Pawn capturedRoyalty = selectedRoyaltyForAbduction;

            activePawn.position = position;

            // Record the captive and turn the Royalty into a Bloodhound.
            AbductRoyalty(capturedRoyalty);

            // Clear the temporary selection state.
            choosingAbductionTarget = false;
            choosingPostAbductionDestination = false;
            selectedRoyaltyForAbduction = null;
            stepsToSelectedRoyalty = 0;
            remainingBanditMovement = 0;

            if (cancelSelectionButton != null)
                cancelSelectionButton.gameObject.SetActive(false);

            // Movement is over; the Bandit can now finish its activation.
            waitingForDestination = false;
            waitingForAction = true;
            hasRolled = false;

            reachable.Clear();
            abductionTargets.Clear();

            RefreshBoard();
            UpdateStatus();
            return;
        }



        // Bloodhound movement: move and sniff after each move.
        if (waitingForDestination &&
            activePawn.type == "Bloodhound" &&
            reachable.Contains(position))
        {
            activePawn.position = position;

            waitingForDestination = false;
            reachable.Clear();

            bloodhoundMovesRemaining--;

            // Sniff immediately after moving.
            SniffForCaptive(activePawn);

            if (bloodhoundMovesRemaining > 0)
            {
                // Prepare the Bloodhound's second movement.
                rolledMovement = 5;
                waitingForDestination = true;

                CalculateReachableTiles();
            }
            else
            {
                // Both movements are complete.
                waitingForAction = true;
                CalculateBloodhoundAttackTargets();
            }

            RefreshBoard();
            UpdateStatus();
            return;
        }

        // Normal movement for other pawns.
        // Normal movement for other pawns.
        if (waitingForDestination && reachable.Contains(position))
        {
            bool wasAtCastle = activePawn.atCastle;
            Vector2Int previousPosition = activePawn.position;
            activePawn.position = position;
            activePawn.atCastle = false;

            // Returning to the team's entry tile can represent the final step
            // into the off-board castle, but only if that extra step fits the roll.
            bool deliveredCaptives = false;
            if (activePawn.type == "Bandit" && !wasAtCastle &&
                position == GetTeamStartPosition(activePawn.team) &&
                HasCaptivesForBandit(activePawn.team))
            {
                Dictionary<Vector2Int, int> returnDistances =
                    GetBanditDistances(previousPosition, rolledMovement);
                int stepsToEntry = returnDistances.TryGetValue(position, out int d) ? d : int.MaxValue;
                if (stepsToEntry + 1 <= rolledMovement && stepsToEntry > 0)
                {
                    activePawn.atCastle = true;
                    deliveredCaptives = DeliverCaptives(activePawn);
                }
            }

            // Keep captured Royalty tokens with their Bandit unless delivered.
            if (activePawn.type == "Bandit" && !deliveredCaptives)
            {
                MoveCapturedRoyaltyWithBandit(activePawn);
            }

            waitingForDestination = false;
            hasRolled = false;
            reachable.Clear();
            abductionTargets.Clear();

            // A rescued Royalty only runs to safety; it gets no action.
            if (rescueRunOnly)
            {
                FinishActivation();
                return;
            }

            waitingForAction = true;
            RefreshBoard();
            UpdateStatus();
        }

    }

    private void RollOrFinish()
    {
        if (activePawn == null || waitingForDestination)
            return;

        // The player may always skip the current pawn's action.
        if (waitingForAction)
        {
            FinishActivation();
            return;
        }

        // Bloodhounds get two movements of up to 5 tiles each.
        if (activePawn.type == "Bloodhound")
        {
            // Record the scent distance BEFORE the first move, so the first
            // sniff can compare the new position with the starting position.
            Captive trackedCaptive = captives.Find(c =>
                c.royaltyTeam == activePawn.team);

            if (trackedCaptive != null)
            {
                Pawn trackedRoyalty = pawns.Find(p =>
                    p.type == "CapturedRoyalty" &&
                    p.team == trackedCaptive.royaltyTeam);

                if (trackedRoyalty != null)
                {
                    previousSniffDistances[activePawn] =
                        Mathf.Abs(trackedRoyalty.position.x - activePawn.position.x) +
                        Mathf.Abs(trackedRoyalty.position.y - activePawn.position.y);
                }
                else
                {
                    previousSniffDistances.Remove(activePawn);
                }
            }
            else
            {
                previousSniffDistances.Remove(activePawn);
            }

            bloodhoundMovesRemaining = 2;
            rolledMovement = 5;
            waitingForDestination = true;

            CalculateReachableTiles();
            UpdateStatus();
            RefreshBoard();
            return;
        }

        if (activePawn.type == "Bandit")
        {
            rolledMovement = 5;
            waitingForDestination = true;

            CalculateReachableTiles();
            UpdateStatus();
            RefreshBoard();
            return;
        }

        if (!hasRolled)
        {
            rolledMovement = movementDie[
                UnityEngine.Random.Range(0, movementDie.Length)
            ];

            hasRolled = true;
            waitingForDestination = true;
            CalculateReachableTiles();

            UpdateStatus();
            RefreshBoard();
        }
    }



    private void CalculateReachableTiles()
    {
        reachable.Clear();

        Queue<Vector2Int> queue = new();
        Dictionary<Vector2Int, int> distance = new();

        // Leaving a castle costs the first movement step and must begin on
        // the team's own entry tile. No castle cells are drawn on the board.
        int initialCost = activePawn.atCastle ? 1 : 0;
        if (activePawn.atCastle && rolledMovement < 1)
            return;

        queue.Enqueue(activePawn.position);
        distance[activePawn.position] = initialCost;
        if (activePawn.atCastle && InBounds(activePawn.position) && !IsTower(activePawn.position))
        {
            if (activePawn.type == "Bandit" || IsGrass(activePawn.position))
                reachable.Add(activePawn.position);
        }

        Vector2Int[] directions =
        {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDistance = distance[current];

            if (currentDistance >= rolledMovement)
                continue;

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;

                if (!InBounds(next) || IsTower(next))
                    continue;

                if (distance.ContainsKey(next))
                    continue;

                if (activePawn.type == "Bandit")
                {
                    // Bandits can pass through any terrain.
                    distance[next] = currentDistance + 1;
                    queue.Enqueue(next);

                    if (IsBanditEndingTerrain(next) ||
                        (!activePawn.atCastle && HasCaptivesForBandit(activePawn.team) &&
                         next == GetTeamStartPosition(activePawn.team) &&
                         currentDistance + 2 <= rolledMovement))
                    {
                        reachable.Add(next);
                    }
                }
                else
                {
                    // Royalty and Squire can only traverse grass.
                    if (!IsGrass(next))
                        continue;

                    distance[next] = currentDistance + 1;
                    queue.Enqueue(next);
                    reachable.Add(next);
                }
            }
        }

        // Staying still is allowed, if the current tile is a legal destination.
        if (!activePawn.atCastle)
        {
            if (activePawn.type == "Bandit")
            {
                if (IsBanditEndingTerrain(activePawn.position))
                    reachable.Add(activePawn.position);
            }
            else if (IsGrass(activePawn.position))
            {
                reachable.Add(activePawn.position);
            }
        }

        CalculateAbductionTargets();
    }

    private void CalculateBloodhoundAttackTargets()
    {
        bloodhoundAttackTargets.Clear();

        if (activePawn == null ||
            activePawn.type != "Bloodhound")
        {
            return;
        }

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                int distance =
                    Mathf.Abs(position.x - activePawn.position.x) +
                    Mathf.Abs(position.y - activePawn.position.y);

                if (distance > 5)
                    continue;

                if (IsBanditEndingTerrain(position))
                {
                    bloodhoundAttackTargets.Add(position);
                }
            }
        }
    }

    private Dictionary<Vector2Int, int> GetBanditDistances(
        Vector2Int start,
        int maxSteps)
    {
        Dictionary<Vector2Int, int> distance = new();
        Queue<Vector2Int> queue = new();

        distance[start] = 0;
        queue.Enqueue(start);

        Vector2Int[] directions =
        {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDistance = distance[current];

            if (currentDistance >= maxSteps)
                continue;

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;

                if (!InBounds(next) || IsTower(next))
                    continue;

                if (distance.ContainsKey(next))
                    continue;

                distance[next] = currentDistance + 1;
                queue.Enqueue(next);
            }
        }

        return distance;
    }

    private HashSet<Vector2Int> GetBanditDestinations(
        Vector2Int start,
        int maxSteps)
    {
        HashSet<Vector2Int> destinations = new();

        if (maxSteps < 0)
            return destinations;

        Dictionary<Vector2Int, int> distances =
            GetBanditDistances(start, maxSteps);

        foreach (KeyValuePair<Vector2Int, int> entry in distances)
        {
            if (IsBanditEndingTerrain(entry.Key))
                destinations.Add(entry.Key);
        }

        return destinations;
    }


    private void CalculateAbductionTargets()
    {
        abductionTargets.Clear();

        if (activePawn == null ||
            activePawn.type != "Bandit" ||
            !waitingForDestination ||
            HasCaptivesForBandit(activePawn.team))
        {
            return;
        }

        Dictionary<Vector2Int, int> distances =
            GetBanditDistances(
                activePawn.position,
                activePawn.atCastle ? rolledMovement - 1 : rolledMovement
            );

        foreach (Pawn pawn in pawns)
        {
            if (pawn.type != "Royalty" ||
                pawn.atCastle ||
                pawn.team == currentTeam)
            {
                continue;
            }

            int castleExitCost = activePawn.atCastle ? 1 : 0;

            if (distances.TryGetValue(pawn.position, out int steps) &&
                steps + castleExitCost > 0 &&
                steps + castleExitCost <= rolledMovement)
            {
                abductionTargets.Add(pawn.position);
            }
        }
    }


    private void AdvancePawn()
    {
        currentPawnIndex++;

        if (currentPawnIndex >= PawnOrder.Length)
        {
            currentPawnIndex = 0;
            currentTeam = (currentTeam + 1) % 4;
        }

        activePawn = GetActivePawn();
    }


    private void RefreshBoard()
    {
        for (int i = 0; i < tileImages.Count; i++)
        {
            if (tileImages[i] == null)
                continue;

            GameObject tile = tileImages[i].gameObject;
            Vector2Int position = tilePositions[i];
            string terrain = GetTerrainAt(position).ToLowerInvariant();
            Image image = tileImages[i];

            image.color = GetTerrainColor(terrain);

            if (reachable.Contains(position))
                image.color = new Color(0.95f, 0.95f, 0.35f);
            else if (abductionTargets.Contains(position))
                image.color = new Color(1f, 0.55f, 0.25f);
            else if (bloodhoundAttackTargets.Contains(position))
                image.color = new Color(0.3f, 0.85f, 1f);

            // Update special-terrain glow objects.
            Transform[] children = tile.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child != tile.transform && child.name.EndsWith("Glow"))
                    child.gameObject.SetActive(
                        terrain == "largetree" || terrain == "lootbush");
            }

            Transform labelTransform = tile.transform.Find("Label");
            Text label = labelTransform != null
                ? labelTransform.GetComponent<Text>()
                : null;

            List<Pawn> occupants = pawns.FindAll(p =>
                !p.atCastle && p.position == position);

            // Remove EVERY old marker container, not just the first one.
            // Disable immediately because Unity's Destroy() is deferred.
            // This also handles multiple RefreshBoard() calls in one frame.
            List<GameObject> oldMarkerObjects = new List<GameObject>();

            for (int childIndex = 0; childIndex < tile.transform.childCount; childIndex++)
            {
                Transform child = tile.transform.GetChild(childIndex);

                if (child.name == "PawnMarkers")
                    oldMarkerObjects.Add(child.gameObject);
            }

            foreach (GameObject oldMarker in oldMarkerObjects)
            {
                if (oldMarker == null)
                    continue;

                oldMarker.SetActive(false);
                Destroy(oldMarker);
            }

            if (occupants.Count > 0)
            {
                if (label != null)
                    label.text = "";

                CreatePawnMarkers(tile.transform, occupants);
            }
            else if (label != null)
            {
                label.color = new Color(0.12f, 0.14f, 0.12f, 0.9f);
                label.fontSize = 14;
                label.fontStyle = FontStyle.Bold;
                label.text = GetTerrainSymbol(terrain);
            }
        }

        // Keep the edge information in sync with resource changes.
        for (int i = 0; i < spawnedTiles.Count; i++)
        {
            GameObject frameCell = spawnedTiles[i];

            if (frameCell == null ||
                frameCell.GetComponent<Button>() != null)
                continue;

            Transform labelTransform = frameCell.transform.Find("Label");
            if (labelTransform == null)
                continue;

            Text frameLabel = labelTransform.GetComponent<Text>();
            if (frameLabel == null)
                continue;

            if (frameCell.name.StartsWith("Castle_Team_"))
                continue;

            int childIndex = frameCell.transform.GetSiblingIndex();
            int visualColumns = columns + 2;
            int visualX = childIndex % visualColumns;
            int visualY = childIndex / visualColumns;

            frameLabel.text = GetFrameStatsText(
                visualX, visualY, visualColumns, rows + 2);
        }
    }


    private void CreatePawnMarkers(Transform tile, List<Pawn> occupants)
    {
        if (occupants == null || occupants.Count == 0)
            return;

        GameObject container = new GameObject(
            "PawnMarkers",
            typeof(RectTransform));

        container.transform.SetParent(tile, false);

        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = new Vector2(3f, 3f);
        containerRect.offsetMax = new Vector2(-3f, -3f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.localScale = Vector3.one;
        containerRect.localRotation = Quaternion.identity;

        // Make sure the markers use the tile's full available area.
        container.transform.SetAsLastSibling();

        RectTransform tileRect = tile.GetComponent<RectTransform>();

        float availableWidth = Mathf.Max(1f, tileRect.rect.width - 6f);
        float availableHeight = Mathf.Max(1f, tileRect.rect.height - 6f);

        int count = occupants.Count;

        // Use a roughly square grid, with enough cells for every pawn.
        int gridColumns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int gridRows = Mathf.CeilToInt((float)count / gridColumns);

        // Leave small gaps between badges.
        float gap = Mathf.Clamp(
            Mathf.Min(availableWidth, availableHeight) * 0.04f,
            1f,
            4f);

        float badgeWidth = (availableWidth - gap * (gridColumns - 1))
                           / gridColumns;

        float badgeHeight = (availableHeight - gap * (gridRows - 1))
                            / gridRows;

        // A small margin prevents badges from touching tile edges.
        float badgeSize = Mathf.Max(
            1f,
            Mathf.Min(badgeWidth, badgeHeight) * 0.88f);

        float totalGridWidth =
            gridColumns * badgeSize + (gridColumns - 1) * gap;

        float totalGridHeight =
            gridRows * badgeSize + (gridRows - 1) * gap;

        float startX = -totalGridWidth / 2f + badgeSize / 2f;
        float startY = totalGridHeight / 2f - badgeSize / 2f;

        for (int i = 0; i < count; i++)
        {
            Pawn pawn = occupants[i];

            int gridX = i % gridColumns;
            int gridY = i / gridColumns;

            GameObject badge = new GameObject(
                "PawnBadge_T" + (pawn.team + 1),
                typeof(RectTransform),
                typeof(Image));

            badge.transform.SetParent(container.transform, false);

            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.localScale = Vector3.one;
            badgeRect.localRotation = Quaternion.identity;
            badgeRect.sizeDelta = new Vector2(badgeSize, badgeSize);

            float x = startX + gridX * (badgeSize + gap);
            float y = startY - gridY * (badgeSize + gap);

            badgeRect.anchoredPosition = new Vector2(x, y);

            Image badgeImage = badge.GetComponent<Image>();
            badgeImage.sprite = GetPawnIconSprite("badge");
            badgeImage.color = TeamColors[pawn.team];
            badgeImage.raycastTarget = false;
            badgeImage.preserveAspect = true;

            string iconName = GetPawnIconName(pawn.type);
            Sprite iconSprite = GetPawnIconSprite(iconName);

            if (iconSprite != null)
            {
                GameObject iconObject = new GameObject(
                    "Icon",
                    typeof(RectTransform),
                    typeof(Image));

                iconObject.transform.SetParent(badge.transform, false);

                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.13f, 0.13f);
                iconRect.anchorMax = new Vector2(0.87f, 0.87f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;

                Image iconImage = iconObject.GetComponent<Image>();
                iconImage.sprite = iconSprite;
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }
            else
            {
                GameObject fallback = new GameObject(
                    "IconFallback",
                    typeof(RectTransform),
                    typeof(Text));

                fallback.transform.SetParent(badge.transform, false);

                RectTransform fallbackRect =
                    fallback.GetComponent<RectTransform>();

                fallbackRect.anchorMin = Vector2.zero;
                fallbackRect.anchorMax = Vector2.one;
                fallbackRect.offsetMin = Vector2.zero;
                fallbackRect.offsetMax = Vector2.zero;

                Text fallbackText = fallback.GetComponent<Text>();
                fallbackText.font =
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                fallbackText.text =
                    GetPawnInitial(pawn.type) + (pawn.team + 1);

                fallbackText.color = Color.white;
                fallbackText.fontStyle = FontStyle.Bold;
                fallbackText.fontSize = Mathf.Clamp(
                    Mathf.RoundToInt(badgeSize * 0.32f), 5, 13);

                fallbackText.alignment = TextAnchor.MiddleCenter;
                fallbackText.raycastTarget = false;
            }

            if (pawn.type == "CapturedRoyalty")
            {
                GameObject captiveLabelObject = new GameObject(
                    "CapturedLabel",
                    typeof(RectTransform),
                    typeof(Text));

                captiveLabelObject.transform.SetParent(badge.transform, false);

                RectTransform captiveRect =
                    captiveLabelObject.GetComponent<RectTransform>();

                captiveRect.anchorMin = new Vector2(0f, 0f);
                captiveRect.anchorMax = new Vector2(1f, 0.35f);
                captiveRect.offsetMin = Vector2.zero;
                captiveRect.offsetMax = Vector2.zero;

                Text captiveLabel =
                    captiveLabelObject.GetComponent<Text>();

                captiveLabel.font =
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                captiveLabel.text = "CAPTIVE";
                captiveLabel.fontSize = Mathf.Clamp(
                    Mathf.RoundToInt(badgeSize * 0.12f), 4, 7);

                captiveLabel.fontStyle = FontStyle.Bold;
                captiveLabel.alignment = TextAnchor.MiddleCenter;
                captiveLabel.color = Color.white;
                captiveLabel.raycastTarget = false;
            }
        }
    }

    private string GetPawnIconName(string type)
    {
        if (type == "Royalty" || type == "CapturedRoyalty") return "royalty";
        if (type == "Squire") return "squire";
        if (type == "Bandit") return "bandit";
        if (type == "Bloodhound") return "bloodhound";
        return "";
    }

    private string GetPawnInitial(string type)
    {
        if (type == "Royalty" || type == "CapturedRoyalty") return "R";
        if (type == "Squire") return "S";
        if (type == "Bandit") return "B";
        if (type == "Bloodhound") return "H";
        return "?";
    }

    private Sprite GetPawnIconSprite(string iconName)
    {
        if (string.IsNullOrEmpty(iconName)) return null;
        if (pawnIconSprites.TryGetValue(iconName, out Sprite cached)) return cached;

        Texture2D texture = Resources.Load<Texture2D>("StruikroversIcons/" + iconName);
        if (texture == null) return null;
        loadedPawnIconTextures[iconName] = texture;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        pawnIconSprites[iconName] = sprite;
        return sprite;
    }

    private void UpdateStatus()
    {
        if (statusText == null || activePawn == null)
            return;

        string teamName = "TEAM " + (currentTeam + 1);
        string pawnName = activePawn.type == "Royalty" ? "ROYALTY" :
            activePawn.type == "Squire" ? "SQUIRE" :
            activePawn.type == "Bandit" ? "BANDIT" :
            activePawn.type == "Bloodhound" ? "BLOODHOUND" : activePawn.type.ToUpperInvariant();
        string message = "<size=120%>TEAM " + (currentTeam + 1) + "  •  " + pawnName + "'S TURN</size>\n";
        if (activePawn.atCastle)
            message += " (at castle; first movement step must enter the board)";

        message += $" | Wood: {teamWood[currentTeam]} | Cards: {teamCards[currentTeam]}";

        if (waitingForDestination)
        {
            if (activePawn.type == "Bloodhound")
            {
                message +=
                    $" | Bloodhound movement: " +
                    $"{bloodhoundMovesRemaining} move(s) remaining. " +
                    "Choose a highlighted empty tile.";
            }
            else if (choosingPostAbductionDestination)
            {
                message +=
                    $" | Abduction preview: {stepsToSelectedRoyalty} steps to Royalty, " +
                    $"{remainingBanditMovement} movement steps left. " +
                    "Choose a highlighted destination or cancel.";
            }
            else
            {
                message += " | Move up to " + rolledMovement +
                           " tiles. Orange Royalties can be abducted; " +
                           "yellow tiles are destinations.";
            }
        }
        else if (waitingForAction)
        {
            if (activePawn.type == "Bloodhound")
            {
                message +=
                    " | Bloodhound attack: choose a highlighted bush or tree " +
                    "within 5 tiles to attack.";
            }
            else
            {
                message += activePawn.type == "Squire"
                ? " | Action: click an adjacent tree to chop it, or finish."
                : " | Action phase: no actions implemented yet. Finish to continue.";
            }
        }
        else if (activePawn.type == "Bandit")
        {
            message += " | Move up to 5 tiles.";
        }
        else
        {
            message += " | Roll to determine movement.";
        }

        if (!string.IsNullOrEmpty(sniffingClue))
        {
            message += "\n" + sniffingClue;
        }

        statusText.text = message;
        if (statusBackground != null)
        {
            Color activeColor = TeamColors[currentTeam];
            statusBackground.color = Color.Lerp(activeColor, new Color(0.08f, 0.09f, 0.12f), 0.48f);
        }

        if (rollButtonText != null)
        {
            if (waitingForAction)
                rollButtonText.text = "Finish Activation";
            else if (waitingForDestination)
                rollButtonText.text = "Choose a highlighted tile";
            else if (activePawn.type == "Bandit")
                rollButtonText.text = "Move Bandit";
            else
                rollButtonText.text = "Roll Movement";
        }

        if (rollButton != null)
            rollButton.interactable = !waitingForDestination;
    }


    private Vector2Int TilePositionFromIndex(int index)
    {
        int x = index % columns;
        int y = index / columns;
        return new Vector2Int(x, y);
    }

    private bool InBounds(Vector2Int p)
    {
        return p.x >= 0 && p.x < columns &&
               p.y >= 0 && p.y < rows;
    }

    private string GetTerrainAt(Vector2Int position)
    {
        if (terrainByPosition.TryGetValue(position, out string terrain))
            return terrain;

        return "grass";
    }

    private bool IsTower(Vector2Int p)
    {
        return GetTerrainAt(p).ToLowerInvariant() == "tower";
    }

    private bool IsGrass(Vector2Int p)
    {
        return GetTerrainAt(p).ToLowerInvariant() == "grass";
    }

    private bool IsBanditEndingTerrain(Vector2Int position)
    {
        string terrain = GetTerrainAt(position).ToLowerInvariant();

        return terrain == "bush" ||
               terrain == "lootbush" ||
               terrain == "tree" ||
               terrain == "largetree";
    }

    private Color GetTerrainColor(string terrain)
    {
        switch (terrain.ToLowerInvariant())
        {
            case "grass":
                return new Color(0.43f, 0.68f, 0.34f);

            case "bush":
                return new Color(0.12f, 0.40f, 0.18f);

            case "tree":
                return new Color(0.25f, 0.48f, 0.25f);

            case "stump":
                return new Color(0.55f, 0.35f, 0.18f);

            case "largetree":
                return new Color(0.12f, 0.32f, 0.16f);

            case "lootbush":
                return new Color(0.12f, 0.40f, 0.18f);

            case "tower":
                return new Color(0.65f, 0.65f, 0.70f);

            default:
                return Color.magenta;
        }
    }

    private string GetTerrainSymbol(string terrain)
    {
        switch (terrain.ToLowerInvariant())
        {
            case "grass": return "";
            case "bush": return "B";
            case "tree": return "T";
            case "stump": return "S";
            case "largetree": return "LT";
            case "lootbush": return "LB";
            case "tower": return "TOWER";
            default: return "?";
        }
    }

    private bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    private bool IsTree(Vector2Int position)
    {
        string terrain = GetTerrainAt(position).ToLowerInvariant();

        return terrain == "tree" || terrain == "largetree";
    }

    private void ChopTree(Vector2Int position)
    {
        string terrain = GetTerrainAt(position).ToLowerInvariant();

        int woodGained = terrain == "largetree" ? 2 : 1;

        teamWood[currentTeam] += woodGained;
        terrainByPosition[position] = "stump";

        Debug.Log(
            "Player " + (currentTeam + 1) +
            " chopped a " + terrain +
            " and gained " + woodGained +
            " wood. Total wood: " + teamWood[currentTeam]
        );

        FinishActivation();
    }

    private void LootBush(Vector2Int position)
    {
        string terrain = GetTerrainAt(position).ToLowerInvariant();

        if (terrain != "bush" && terrain != "lootbush")
            return;

        int cardsGained = terrain == "lootbush" ? 2 : 1;
        teamCards[currentTeam] += cardsGained;

        // Change special loot bush into a normal bush.
        if (terrain == "lootbush")
        {
            terrainByPosition[position] = "bush";
        }

        Debug.Log(
            $"Team {currentTeam + 1} looted a bush and gained " +
            $"{cardsGained} card(s)."
        );

        RefreshBoard();
        UpdateStatus();
        FinishActivation();
    }

    private void FinishActivation()
    {
        waitingForAction = false;
        waitingForDestination = false;
        hasRolled = false;
        reachable.Clear();
        abductionTargets.Clear();
        bloodhoundAttackTargets.Clear();
        rescueRunOnly = false;

        if (resolvingRescueTurns)
        {
            StartNextRescueRun();
            return;
        }

        AdvancePawn();
        RefreshBoard();
        UpdateStatus();
    }

    private void ResolveBloodhoundAttack(Vector2Int attackedPosition)
    {
        List<Pawn> banditsFound = pawns.FindAll(p =>
            p.type == "Bandit" && p.position == attackedPosition);

        List<Pawn> rescuedRoyalties = new();
        HashSet<Vector2Int> reservedDestinations = new();
        int releasedCount = 0;

        foreach (Pawn bandit in banditsFound)
        {
            // The Bloodhound cannot free captives held by its own team.
            if (bandit.team == activePawn.team)
                continue;

            // A Bandit can carry only one Royalty.
            Captive captive = captives.Find(c =>
                c.banditTeam == bandit.team);

            // A Bandit without a captive has nothing to rescue.
            if (captive == null)
                continue;

            Pawn capturedToken = pawns.Find(p =>
                p.type == "CapturedRoyalty" &&
                p.team == captive.royaltyTeam);

            Pawn royalty = pawns.Find(p =>
                p.team == captive.royaltyTeam &&
                p.type == "Bloodhound");

            if (royalty == null)
                continue;

            Vector2Int? destination = FindNearestFreeRoyaltyTile(
                attackedPosition, reservedDestinations);

            if (!destination.HasValue)
            {
                Debug.LogWarning(
                    $"Could not find an empty grass tile to release " +
                    $"Player {royalty.team + 1}'s Royalty."
                );

                // Leave the captive and its token intact if there is
                // nowhere legal to place the original Royalty pawn.
                continue;
            }

            // Release the original Royalty.
            royalty.type = "Royalty";
            royalty.position = destination.Value;
            royalty.atCastle = false;

            reservedDestinations.Add(destination.Value);
            rescuedRoyalties.Add(royalty);
            previousSniffDistances.Remove(royalty);
            releasedCount++;

            // Remove the carried token and captive record.
            if (capturedToken != null)
                pawns.Remove(capturedToken);

            captives.Remove(captive);

            // Return the rescued Bandit to its own off-board castle.
            bandit.position = GetTeamStartPosition(bandit.team);
            bandit.atCastle = true;
        }

        bloodhoundAttackTargets.Clear();

        if (releasedCount == 0)
        {
            sniffingClue = banditsFound.Count == 0
                ? "The Bloodhound attacks the hiding place, but finds no Bandit."
                : "The Bloodhound finds a Bandit, but no captives are released.";

            Debug.Log(sniffingClue);
            FinishActivation();
            return;
        }

        sniffingClue =
            $"The Bloodhound found a Bandit! {releasedCount} captive(s) escaped.";

        Debug.Log(sniffingClue);

        interruptedBloodhound = activePawn;
        interruptedTeam = currentTeam;
        interruptedPawnIndex = currentPawnIndex;
        resolvingRescueTurns = true;

        foreach (Pawn royalty in rescuedRoyalties)
            pendingRescueRoyalties.Enqueue(royalty);

        RefreshBoard();
        StartNextRescueRun();
    }

    private void StartNextRescueRun()
    {
        waitingForAction = false;
        waitingForDestination = false;
        hasRolled = false;
        reachable.Clear();
        abductionTargets.Clear();
        bloodhoundAttackTargets.Clear();
        rescueRunOnly = false;

        if (pendingRescueRoyalties.Count > 0)
        {
            Pawn royalty = pendingRescueRoyalties.Dequeue();
            currentTeam = royalty.team;
            currentPawnIndex = 0; // Royalty is the first pawn in PawnOrder.
            activePawn = royalty;
            rescueRunOnly = true;
            sniffingClue = $"Player {royalty.team + 1}'s Royalty escaped and must run to safety.";
            RefreshBoard();
            UpdateStatus();
            return;
        }

        // All freed Royalty have run. Resume and finish the interrupted
        // Bloodhound activation, rather than advancing from the last rescuer.
        resolvingRescueTurns = false;
        currentTeam = interruptedTeam;
        currentPawnIndex = interruptedPawnIndex;
        activePawn = interruptedBloodhound;
        interruptedBloodhound = null;
        waitingForAction = false;
        waitingForDestination = false;
        hasRolled = false;
        reachable.Clear();
        FinishActivation();
    }

    private Vector2Int GetTeamStartPosition(int team)
    {
        switch (team)
        {
            case 0: return new Vector2Int(columns / 2, 0);
            case 1: return new Vector2Int(columns - 1, rows / 2);
            case 2: return new Vector2Int(columns / 2, rows - 1);
            default: return new Vector2Int(0, rows / 2);
        }
    }

    private Vector2Int? FindNearestFreeRoyaltyTile(
        Vector2Int origin,
        HashSet<Vector2Int> reserved)
    {
        Queue<Vector2Int> queue = new();
        HashSet<Vector2Int> visited = new();
        Vector2Int[] directions =
        {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };

        queue.Enqueue(origin);
        visited.Add(origin);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (IsInsideBoard(current) && IsGrass(current) &&
                !IsTower(current) && !reserved.Contains(current) &&
                !pawns.Exists(p => !p.atCastle && p.position == current &&
                    p.type != "CapturedRoyalty"))
            {
                return current;
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (!IsInsideBoard(next) || !visited.Add(next))
                    continue;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    private bool IsInsideBoard(Vector2Int position)
    {
        return position.x >= 0 && position.x < columns &&
               position.y >= 0 && position.y < rows;
    }

    private void CreateGlowOutline(GameObject tile, string terrain)
    {
        Color glowColor;

        switch (terrain.ToLowerInvariant())
        {
            case "largetree":
                glowColor = new Color(0.35f, 1f, 0.45f);
                break;

            case "lootbush":
                glowColor = new Color(1f, 0.35f, 0.85f);
                break;

            default:
                return;
        }

        // Four bright edges create a simple outline.
        CreateGlowEdge(tile, "TopGlow", glowColor,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -3f), new Vector2(0f, 0f));

        CreateGlowEdge(tile, "BottomGlow", glowColor,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, 3f));

        CreateGlowEdge(tile, "LeftGlow", glowColor,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(3f, 0f));

        CreateGlowEdge(tile, "RightGlow", glowColor,
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-3f, 0f), new Vector2(0f, 0f));
    }

    private void CreateGlowEdge(
        GameObject tile,
        string edgeName,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject edge = new GameObject(
            edgeName, typeof(RectTransform), typeof(Image));

        edge.transform.SetParent(tile.transform, false);

        RectTransform rect = edge.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image image = edge.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        // Keep the outline above the terrain image but below its text label.
        edge.transform.SetAsLastSibling();

        glowImages.Add(image);
        glowColors.Add(color);
    }



    private void AbductRoyalty(Pawn royalty, Pawn capturingBandit = null)
    {
        if (royalty == null || royalty.type != "Royalty")
            return;

        // Existing Bandit actions default to the active pawn. Bush ambushes
        // pass the hidden Bandit explicitly because Royalty is active then.
        if (capturingBandit == null)
            capturingBandit = activePawn;

        if (capturingBandit == null ||
            capturingBandit.type != "Bandit" ||
            royalty.team == capturingBandit.team)
        {
            return;
        }

        // A Bandit can carry only one Royalty at a time.
        if (HasCaptivesForBandit(capturingBandit.team))
        {
            Debug.Log(
                $"Player {capturingBandit.team + 1}'s Bandit is already carrying a Royalty."
            );
            return;
        }

        int royaltyTeam = royalty.team;
        Vector2Int originalPosition = royalty.position;

        captives.Add(new Captive
        {
            royaltyTeam = royaltyTeam,
            banditTeam = capturingBandit.team
        });

        // Place the captured Royalty token with the Bandit.
        pawns.Add(new Pawn
        {
            team = royaltyTeam,
            type = "CapturedRoyalty",
            position = capturingBandit.position
        });

        // The original Royalty becomes the Bloodhound.
        // It stays where the abduction began.
        royalty.type = "Bloodhound";
        royalty.position = originalPosition;

        // Reset the clue tracking for this Bloodhound.
        previousSniffDistances.Remove(royalty);
        sniffingClue = "The Bloodhound is ready to pick up the scent.";

        Debug.Log(
            $"Player {capturingBandit.team + 1} abducted " +
            $"Player {royaltyTeam + 1}'s Royalty!"
        );

        RefreshBoard();
        UpdateStatus();
    }

    private bool HasCaptivesForBandit(int banditTeam)
    {
        return captives.Exists(c => c.banditTeam == banditTeam);
    }

    private bool DeliverCaptives(Pawn bandit)
    {
        if (bandit == null || bandit.type != "Bandit")
            return false;

        List<Captive> delivered = captives.FindAll(c => c.banditTeam == bandit.team);
        if (delivered.Count == 0)
            return false;

        int totalWoodStolen = 0;
        foreach (Captive captive in delivered)
        {
            // Remove the carried token and the Bloodhound. Restore the Royalty
            // pawn at their own castle so it can leave on their next turn.
            Pawn carriedToken = pawns.Find(p => p.type == "CapturedRoyalty" && p.team == captive.royaltyTeam);
            if (carriedToken != null)
                pawns.Remove(carriedToken);

            Pawn royaltyOrBloodhound = pawns.Find(p => p.team == captive.royaltyTeam && p.type == "Bloodhound");
            if (royaltyOrBloodhound != null)
            {
                royaltyOrBloodhound.type = "Royalty";
                royaltyOrBloodhound.position = GetTeamStartPosition(captive.royaltyTeam);
                royaltyOrBloodhound.atCastle = true;
                previousSniffDistances.Remove(royaltyOrBloodhound);
            }

            int stolen = Mathf.Min(2, teamWood[captive.royaltyTeam]);
            teamWood[captive.royaltyTeam] -= stolen;
            totalWoodStolen += stolen;
            captives.Remove(captive);
        }

        teamWood[bandit.team] += totalWoodStolen;
        sniffingClue = $"Player {bandit.team + 1} delivered {delivered.Count} Royalty and stole {totalWoodStolen} wood.";
        Debug.Log(sniffingClue + " Equipment and a discard-pile card reward will be added later.");
        RefreshBoard();
        UpdateStatus();
        return true;
    }

    private void MoveCapturedRoyaltyWithBandit(Pawn bandit)
    {
        if (bandit == null || bandit.type != "Bandit")
            return;

        foreach (Captive captive in captives)
        {
            // Only move captives carried by this Bandit's team.
            if (captive.banditTeam != bandit.team)
                continue;

            Pawn capturedRoyalty = pawns.Find(p =>
                p.type == "CapturedRoyalty" &&
                p.team == captive.royaltyTeam);

            if (capturedRoyalty != null)
            {
                capturedRoyalty.position = bandit.position;
            }
        }
    }


    private void SniffForCaptive(Pawn bloodhound)
    {
        if (bloodhound == null ||
            bloodhound.type != "Bloodhound")
        {
            return;
        }

        // Find the captive belonging to this Bloodhound.
        Captive captive = captives.Find(c =>
            c.royaltyTeam == bloodhound.team);

        if (captive == null)
        {
            sniffingClue = "No captive to track.";
            return;
        }

        // Find that captured Royalty token.
        Pawn capturedRoyalty = pawns.Find(p =>
            p.type == "CapturedRoyalty" &&
            p.team == captive.royaltyTeam);

        if (capturedRoyalty == null)
        {
            sniffingClue = "The scent has gone cold.";
            return;
        }

        Vector2Int difference =
            capturedRoyalty.position - bloodhound.position;

        // A 3x3 area includes the surrounding eight tiles
        // and the Bloodhound's own tile.
        if (Mathf.Abs(difference.x) <= 1 &&
            Mathf.Abs(difference.y) <= 1)
        {
            sniffingClue =
                "The Bloodhound senses the captive nearby!";
        }
        else
        {
            int distance =
                Mathf.Abs(difference.x) +
                Mathf.Abs(difference.y);

            if (previousSniffDistances.TryGetValue(
                bloodhound, out int previousDistance))
            {
                if (distance < previousDistance)
                {
                    sniffingClue = "The scent is getting stronger!";
                }
                else if (distance > previousDistance)
                {
                    sniffingClue = "The scent is growing fainter...";
                }
                else
                {
                    sniffingClue = "The scent hasn't changed.";
                }
            }
            else
            {
                sniffingClue =
                    "The Bloodhound catches a scent, but its direction is unclear.";
            }
        }

        // Remember this distance for the next sniff.
        previousSniffDistances[bloodhound] =
            Mathf.Abs(difference.x) + Mathf.Abs(difference.y);

        UpdateStatus();
    }


    private Pawn GetRoyaltyAt(Vector2Int position)
    {
        return pawns.Find(p =>
            p.type == "Royalty" &&
            !p.atCastle &&
            p.position == position);
    }

    private void CancelAbductionSelection()
    {
        if (!choosingAbductionTarget &&
            !choosingPostAbductionDestination)
        {
            return;
        }

        choosingAbductionTarget = false;
        choosingPostAbductionDestination = false;

        selectedRoyaltyForAbduction = null;
        stepsToSelectedRoyalty = 0;
        remainingBanditMovement = 0;

        if (cancelSelectionButton != null)
            cancelSelectionButton.gameObject.SetActive(false);

        // Restore the original Bandit movement options.
        CalculateReachableTiles();

        RefreshBoard();
        UpdateStatus();
    }
}
