using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    #region Settings and References
    public static bool easyMode = false;

    public Transform cameraTansform;
    public GridPlayer player;
    public Enemy testEnemy;
    public Text scoreText;
    public AnimationCurve cameraSpeedCurve = new AnimationCurve();
    public bool startInMiddle = false;
    [Min(3)]
    public int maxChunks = 3;
    public float musicBeatRate = 0.246f;
    public float firstBeat = 0.5f;
    public int beatsPerPlayerMove = 2;
    public int beatsPerEnemyMove = 4;
    public const float gridScale = 0.5f;

    public GameObject defaultTile;

    public string tilesPath = "/Prefabs/Tiles/";
    public List<GameObject> tilePrefabs = new List<GameObject>();
    public bool showPathfindingGizmos = false;
    public bool showBeatsAnalysis = false;
    public float beatsXScale = 3f;
    public float beatsYScale = 10f;
    public float beatsSize = 0.5f;
    public Vector2 beatsOrigin = new Vector2();

    public List<float> beatTimes = new List<float>();
    public List<Vector2Int> brokenPathVals = new List<Vector2Int>();
    private bool pathIsBroken = false;
    #endregion

    #region Controller Variables
    private List<SimpleTile>[] sortedTiles = new List<SimpleTile>[4];
    
    private bool playerDead = false;
    private float nextBeat = 0;
    private int nextManualBeat = 0;
    private int numBeats = 0;
    private AudioSource musicPlayer;

    private List<Tile[]> generatedTiles = new List<Tile[]>();
    private List<Vector2> downConnections = new List<Vector2>();

    private List<Enemy> enemies = new List<Enemy>();

    public float score = 0;
    #endregion

    #region Unity Events
    // Start is called before the first frame update
    void Start()
    {
        SortTilePrefabs();
        musicPlayer = GetComponent<AudioSource>();
        nextBeat = firstBeat;

        if (easyMode)
            beatsPerEnemyMove = 2;
        else
            beatsPerEnemyMove = 4;

        testEnemy.transform.position = player.transform.position + (Vector3.up * gridScale * 2);
        enemies.Add(testEnemy);

        GenerateStartingTiles();
    }

    // Update is called once per frame
    void Update()
    {
        if (!musicPlayer.isPlaying && Time.time >= firstBeat)
            musicPlayer.Play();

        if (musicPlayer.time >= nextBeat)
        {
            nextBeat += musicBeatRate;
            numBeats += 1;
            if (easyMode && beatsPerPlayerMove != 0 && numBeats % beatsPerPlayerMove == 0)
                player.FixedMove();

            if (player.transform.position.y < chunks[chunks.Count - 2].holder.position.y)
                GenerateTileChunk();

            if (easyMode == true)
            {
                //Move on main-beats (only first in set)
                if (beatsPerEnemyMove != 0 && numBeats % beatsPerEnemyMove == 0)
                    MoveEnemies();
            }
            else
            {
                //Move on off-beats (all beats except first)
                if (beatsPerEnemyMove != 0 && numBeats % beatsPerEnemyMove != 0)
                    MoveEnemies();
            }
        }

        MoveCameraToPlayer();

        if (!easyMode)
            PlayerMoveAndScore();
    }

    private void OnDrawGizmos()
    {
        if (showBeatsAnalysis)
            AnalyzeBeats();

        if (showPathfindingGizmos)
        {
            foreach (Chunk c in chunks)
            {
                c.TempDrawNodes();
            }
            DrawPlayerAndMouseNodes();
        }

        DrawBrokenPath();
    }

    void AnalyzeBeats()
    {
        if (beatTimes.Count < 2)
            return;

        float lastBeat = beatTimes[0];
        float totalDifference = 0;
        float minDifference = 100;
        float maxDifference = 0;
        foreach (float beat in beatTimes)
        {
            Vector2 point = beatsOrigin + Vector2.right * beat * beatsXScale;
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(point, beatsSize);
            float difference = beat - lastBeat;
            if (difference < minDifference)
                minDifference = difference;
            totalDifference += difference;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(point, point + Vector2.up * difference * beatsYScale);
            lastBeat = beat;
        }
        Vector2 guideLinePos = Vector2.up * (beatTimes[1] - beatTimes[0]) * beatsYScale;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(beatsOrigin + guideLinePos, beatsOrigin + guideLinePos + Vector2.right * beatTimes[beatTimes.Count - 1] * beatsXScale);
        float averageDifference = totalDifference / (beatTimes.Count - 1);
        Vector2 averageLinePos = Vector2.up * averageDifference * beatsYScale;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(beatsOrigin + averageLinePos, beatsOrigin + averageLinePos + Vector2.right * beatTimes[beatTimes.Count - 1] * beatsXScale);

        Vector2 fixedBeatsLine = Vector2.up * musicBeatRate * beatsYScale;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(beatsOrigin + fixedBeatsLine, beatsOrigin + fixedBeatsLine + Vector2.right * beatTimes[beatTimes.Count - 1] * beatsXScale);

        //Draw the current beat over the human beats:

        Gizmos.color = Color.white;
        Vector2 start = beatsOrigin + Vector2.right * beatTimes[0] * beatsXScale;
        for (int i = 0; i < beatTimes.Count - 1; ++i)
        {
            Vector2 point = start + Vector2.right * i * musicBeatRate * beatsXScale;
            Gizmos.DrawLine(point, point + averageLinePos);
        }
    }
    #endregion

    #region Player and Enemies
    private void PlayerMoveAndScore()
    {

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 delta = mousePos - player.transform.position;

            Vector3 move = Vector2.zero;
            //If the tap was not too close to the player
            if (delta.sqrMagnitude > Mathf.Pow(gridScale * 3f, 2))
            {
                if (delta.x > delta.y) //Right or Down
                {
                    if (delta.x > -delta.y) //Right
                        move = Vector2.right;
                    else //Down
                        move = Vector2.down;
                }
                else //Up or Left
                {
                    if (delta.x > -delta.y) //Up
                        move = Vector2.up;
                    else //Left
                        move = Vector2.left;
                }
            }

            move *= gridScale;

            if (!Physics2D.OverlapPoint(player.transform.position + move))
            {
                player.transform.position += move;

                //Score the move
                float diff = nextBeat - musicPlayer.time;
                float moveScore = ((musicBeatRate / 5) - Mathf.PingPong(diff, musicBeatRate / 2)) / (musicBeatRate / 5);
                moveScore = Mathf.Floor(moveScore * 100);
                //Debug.Log("Diff = " + diff + ", Score = " + moveScore);
                score += moveScore;
                scoreText.text = score.ToString();
            }
        }
    }

    private void MoveEnemies()
    {
        foreach (Enemy E in enemies)
        {
            E.path = GetPathAStar(E.transform.position, player.transform.position);
            E.Move(player.transform.position);
        }
    }
    #endregion

    #region Tiles and Chunks
    private List<Chunk> chunks = new List<Chunk>();
    private int numChunks = 0;

    private void SortTilePrefabs()
    {
        for (int i = 0; i < 4; ++i)
        {
            sortedTiles[i] = new List<SimpleTile>();
        }

        tilePrefabs.AddRange(Resources.LoadAll<GameObject>(tilesPath));

        foreach (GameObject pre in tilePrefabs)
        {
            SimpleTile tile = pre.GetComponent<SimpleTile>();
            for (int i = 0; i < 4; ++i)
            {
                if (tile.connections[i])
                    sortedTiles[i].Add(tile);
            }
        }
    }
    
    private void GenerateStartingTiles()
    {
        CreateCapChunk();
        GenerateTileChunk();
        GenerateTileChunk();

        //Place the player in the first tile of the first chunk
        player.transform.position = chunks[0].slots[chunks[0].startSlot.x, chunks[0].startSlot.y].transform.position;
        //Place the camera two tiles above the player so it can move and imply the motion
        cameraTansform.position = player.transform.position + Vector3.up * SimpleTile.heightInCells * gridScale * 2f;
    }

    private void CreateCapChunk()
    {
        Vector2 chunkPos = new Vector2(0, numChunks * -1 * SimpleTile.heightInCells * gridScale);
        Chunk thisChunk = new Chunk(transform, chunkPos, ++numChunks);

        int startingTile = Chunk.Width / 2;
        if (startInMiddle == false)
            startingTile = Random.Range(0, Chunk.Width);
        thisChunk.startSlot = new Vector2Int(startingTile, 0);
        thisChunk.endSlot = new Vector2Int(startingTile, 0);

        thisChunk.slots = new SimpleTile[Chunk.Width, 1];

        bool[,][] connectionsMatrix = new bool[Chunk.Width, 1][];
        bool lastTileWantsConnection = false;
        for (int x = 0; x < Chunk.Width; ++x)
        {
            connectionsMatrix[x, 0] = new bool[4];
            connectionsMatrix[x, 0][(int)Dir.bottom] = true;
            if (lastTileWantsConnection)
                connectionsMatrix[x, 0][(int)Dir.left] = true;

            lastTileWantsConnection = false;
            if (x < Chunk.Width - 1 && Chunk.Width > 1 && Random.value > 0.33f)
            {
                connectionsMatrix[x, 0][(int)Dir.right] = true;
                lastTileWantsConnection = true;
            }
        }

        FillChunkWithTiles(thisChunk, connectionsMatrix);

        thisChunk.GenerateNodes();

        chunks.Add(thisChunk);
    }

    private void GenerateTileChunk()
    {
        #region Initialize Chunk
        Vector2 chunkPos = new Vector2(0, numChunks * - 1 * Chunk.Height * SimpleTile.heightInCells * gridScale);
        Chunk thisChunk = new Chunk(transform, chunkPos, ++numChunks);

        thisChunk.startSlot = new Vector2Int(0, Chunk.Height - 1);
        if (chunks.Count > 0)
            thisChunk.startSlot.x = chunks[chunks.Count - 1].endSlot.x;
        else
            thisChunk.startSlot.x = Chunk.Width / 2;

        thisChunk.endSlot = new Vector2Int(Random.Range(0, Chunk.Width), 0);
        int x = 0;
        int y = 0;
        #endregion

        #region Invent Path
        //Generate a path between these two by adding random directions that still have a path to the end point.
        Dir[] testPath = GenerateRandomChunkPath(thisChunk.startSlot, thisChunk.endSlot);
        //Dir[] testPath = GenerateChunkPath(thisChunk.startSlot, thisChunk.endSlot);

        bool[,][] connectionsMatrix = new bool[Chunk.Width, Chunk.Height][];
        bool[,] filledTiles = new bool[Chunk.Width, Chunk.Height];
        for (x = 0; x < connectionsMatrix.GetLength(0); ++x)
        {
            for (y = 0; y < connectionsMatrix.GetLength(1); ++y)
                connectionsMatrix[x, y] = new bool[4];
        }
        Vector2Int currentTile = thisChunk.startSlot; //used to put the tile in the correct slot
        for (int i = 0; i <= testPath.Length; ++i)
        {
            //Define required connections
            Dir entrance = Dir.top;
            Dir exit = Dir.bottom;
            if (i != 0)
                entrance = testPath[i - 1].Opposite();

            if (i < testPath.Length)
                exit = testPath[i];
            
            bool[] requiredConns = new bool[4];
            connectionsMatrix[currentTile.x, currentTile.y][(int)entrance] = true;
            connectionsMatrix[currentTile.x, currentTile.y][(int)exit] = true;

            //Choose two random unnecessary connections to add to this slot (if they would connect within the chunk)
            int randomDir = Random.Range(0, 4);
            Vector2Int offset = currentTile + ((Dir)randomDir).ToOffset();
            if (offset.x < Chunk.Width && offset.x >= 0 &&
                offset.y < Chunk.Height && offset.y >= 0)
                connectionsMatrix[currentTile.x, currentTile.y][randomDir] = true;

            /*
            randomDir = Random.Range(0, 4);
            offset = currentTile + ((Dir)randomDir).ToOffset();
            if (offset.x < Chunk.Width && offset.x >= 0 &&
                offset.y < Chunk.Height && offset.y >= 0)
                connectionsMatrix[currentTile.x, currentTile.y][randomDir] = true;
            */

            filledTiles[currentTile.x, currentTile.y] = true;

            //Move the currentTile pointer to the next slot in the path
            currentTile += exit.ToOffset();
        }
        #endregion

        #region Randomize Remaining Tiles
        for (x = 0; x < Chunk.Width; ++x)
        {
            for (y = 0; y < Chunk.Height; ++y)
            {
                if (filledTiles[x, y] == false)
                {
                    bool[] neededDirections = new bool[4];
                    for (int dir = 0; dir < 4; ++dir)
                    {
                        Vector2Int offset = ((Dir)dir).ToOffset();
                        offset.x += x;
                        offset.y += y;
                        if (offset.x >= Chunk.Width || offset.x < 0 ||
                            offset.y >= Chunk.Height || offset.y < 0)
                            continue;

                        if (filledTiles[offset.x, offset.y] == false)
                            neededDirections[dir] = Random.value > 0.5f;
                        else if (connectionsMatrix[offset.x, offset.y][dir.OppositeIfDir()])
                            neededDirections[dir] = true;
                    }
                    connectionsMatrix[x, y] = neededDirections;
                    filledTiles[x, y] = true;
                }
            }
        }
        #endregion

        FillChunkWithTiles(thisChunk, connectionsMatrix);

        thisChunk.GenerateNodes();

        chunks.Add(thisChunk);

        if (chunks.Count > maxChunks + 1)
        {
            Destroy(chunks[1].holder.gameObject);
            chunks.RemoveAt(1);
            chunks[0].holder.Translate(Vector2.down * Chunk.Height * SimpleTile.heightInCells * gridScale);
        }
    }

    private Dir[] GenerateChunkPath(Vector2Int startSlot, Vector2Int endSlot)
    {
        List<Dir> testPath = new List<Dir>();
        int x = startSlot.x;
        int y = startSlot.y;

        //Temp path that just makes the simplest L shape to the end
        while (x != endSlot.x)
        {
            if (x > 100 || x < -100)
                break;
            if (x < endSlot.x)
            {
                x += 1;
                testPath.Add(Dir.right);
            }
            else
            {
                x -= 1;
                testPath.Add(Dir.left);
            }
        }
        while (y != endSlot.y)
        {
            if (y > 100 || y < -100)
                break;
            if (y > endSlot.y)
            {
                y -= 1;
                testPath.Add(Dir.bottom);
            }
            else
            {
                y += 1;
                testPath.Add(Dir.top);
            }
        }
        return testPath.ToArray();
    }

    private Dir[] GenerateRandomChunkPath(Vector2Int startSlot, Vector2Int endSlot)
    {
        if (pathIsBroken)
            return new Dir[] { Dir.bottom };

        List<Dir> path = new List<Dir>();
        bool[,] env = new bool[Chunk.Width, Chunk.Height];

        Vector2Int currentTile = startSlot;
        env[currentTile.x, currentTile.y] = true;

        List<Dir> options = new List<Dir>();
        List<Vector2Int> nodes = new List<Vector2Int>();
        
        pathIsBroken = true;
        brokenPathVals.Add(currentTile);

        int maxLoops = 1000;
        while (--maxLoops > 0)
        {
            //Finish once the end slot is reached
            if (currentTile == endSlot)
                break;

            //Setup the options around the current slot
            options.Clear();
            for (int dir = 0; dir < 4; ++dir)
            {
                Vector2Int offset = ((Dir)dir).ToOffset();
                offset.x += currentTile.x;
                offset.y += currentTile.y;
                if (offset.x >= Chunk.Width || offset.x < 0 ||
                    offset.y >= Chunk.Height || offset.y < 0)
                    continue;
                if (env[offset.x, offset.y])
                    continue;

                options.Add((Dir)dir);
            }
            
            //Find a random slot that has a path to the end slot
            for (int i = 0; i < 3; ++i)
            {
                int choice = Random.Range(0, options.Count);
                nodes.Clear();
                nodes.Add(currentTile + options[choice].ToOffset());
                int nextNode = 0;
                bool hasPath = false;
                int safety = 1000;
                //Check if this option is already the target
                if (currentTile + options[choice].ToOffset() == endSlot)
                    hasPath = true;
                //Do a simple BFS by checking each node until there are none left or the target is found
                while (nextNode < nodes.Count && hasPath == false)
                {
                    if (safety-- < 0)
                        break;
                    Vector2Int currentNode = nodes[nextNode];
                    nextNode++;
                    for (int dir = 0; dir < 4; ++dir)
                    {
                        Vector2Int offset = ((Dir)dir).ToOffset();
                        offset.x += currentNode.x;
                        offset.y += currentNode.y;

                        if (offset == endSlot)
                        {
                            hasPath = true;
                            break;
                        }

                        if (offset.x >= Chunk.Width || offset.x < 0 ||
                            offset.y >= Chunk.Height || offset.y < 0)
                            continue;
                        if (env[offset.x, offset.y])
                            continue;

                        if (nodes.Contains(offset))
                            continue;

                        nodes.Add(offset);
                    }

                    if (hasPath)
                        break;
                }

                //If the BFS found the goal: add the random choice to the path, otherwise remove it from the options list
                if (hasPath)
                {
                    path.Add(options[choice]);
                    currentTile += options[choice].ToOffset();
                    brokenPathVals.Add(currentTile);
                    env[currentTile.x, currentTile.y] = true;
                    break;
                }
                else
                    options.RemoveAt(choice);
            }
        }

        pathIsBroken = false;
        brokenPathVals.Clear();

        //Return the path.

        return path.ToArray();
    }

    private void FillChunkWithTiles(Chunk thisChunk, bool[,][] connections)
    {
        Vector2 tileSize = new Vector2(SimpleTile.widthInCells, SimpleTile.heightInCells) * gridScale;
        Vector2 halfTile = new Vector2(SimpleTile.widthInCells * gridScale * 0.5f, SimpleTile.heightInCells * gridScale * 0.5f);

        for (int x = 0; x < connections.GetLength(0); ++x)
        {
            for (int y = 0; y < connections.GetLength(1); ++y)
            {
                GameObject chosenTile = FindMatchingTile(connections[x,y]);

                //Instantiate the chosen tile and add to the chunk
                Vector2 tilePos = (new Vector2(x, y)).MultipliedBy(tileSize) + (Vector2)thisChunk.holder.position + halfTile;

                SimpleTile newTile = Instantiate(chosenTile, tilePos, Quaternion.identity, thisChunk.holder).GetComponent<SimpleTile>();
                thisChunk.slots[x, y] = newTile;
            }
        }
    }

    private GameObject FindMatchingTile(bool[] requiredConnections, bool allowExtraConnections = false)
    {
        int entrance = -1;
        for (int dir = 0; dir < 4; ++dir)
        {
            if (requiredConnections[dir])
            {
                entrance = dir;
                break;
            }
        }

        if (entrance == -1 || entrance > sortedTiles[entrance].Count)
            return defaultTile;

        bool foundPerfectTile = false;
        GameObject chosenTile = defaultTile;
        foreach (SimpleTile tilePre in sortedTiles[entrance])
        {
            bool isAMatch = true;
            for (int dir = 0; dir < 4; ++dir)
            {
                if (requiredConnections[dir] == true)
                {
                    if (tilePre.connections[dir] == false)
                        isAMatch = false;
                }
                else if (allowExtraConnections == false && tilePre.connections[dir] == true)
                    isAMatch = false;
            }

            if (isAMatch)
                return tilePre.gameObject;
        }

        Debug.LogError("Couldn't find a tile that matches these connections:"
            + " top = " + requiredConnections[(int)Dir.top]
            + ", right = " + requiredConnections[(int)Dir.right]
            + ", bottom = " + requiredConnections[(int)Dir.bottom]
            + ", left = " + requiredConnections[(int)Dir.left]);
        int rand = Random.Range(0, sortedTiles[entrance].Count);
        return sortedTiles[entrance][rand].gameObject;
    }

    public class Chunk
    {
        public static int Width = 5;
        public static int Height = 5;

        public Transform holder;
        public SimpleTile[,] slots = new SimpleTile[Width, Height];
        public Vector2Int endSlot = Vector2Int.zero;
        public Vector2Int startSlot = Vector2Int.zero;

        public bool[,] nodes;
        public Vector2Int bottomConnectNode = Vector2Int.zero;
        public Vector2Int topConnectNode = Vector2Int.zero;

        public Chunk(Transform parent, Vector2 pos, int number)
        {
            holder = new GameObject("Chunk " + number).transform;
            holder.SetParent(parent);
            holder.position = pos;
        }

        public void GenerateNodes()
        {
            nodes = new bool[slots.GetLength(0) * SimpleTile.widthInCells, slots.GetLength(1) * SimpleTile.heightInCells];

            Vector2 origin = (Vector2)holder.position + Vector2.one * gridScale * 0.5f;
            for (int y = 0; y < nodes.GetLength(1); ++y)
            {
                for (int x = 0; x < nodes.GetLength(0); ++x)
                {
                    if (!Physics2D.OverlapPoint(origin + new Vector2(x, y) * gridScale))
                        nodes[x, y] = true;
                }
            }

            bottomConnectNode = new Vector2Int(Mathf.FloorToInt((endSlot.x + 0.5f) * SimpleTile.widthInCells), 0);
            topConnectNode = new Vector2Int(Mathf.FloorToInt((startSlot.x + 0.5f) * SimpleTile.widthInCells), nodes.GetLength(1));
        }

        public void TempDrawNodes()
        {
            Gizmos.color = Color.red;
            Vector2 origin = (Vector2)holder.position + Vector2.one * gridScale * 0.5f;
            for (int x = 0; x < nodes.GetLength(0); ++x)
            {
                for (int y = 0; y < nodes.GetLength(1); ++y)
                {
                    if (nodes[x, y])
                        Gizmos.DrawSphere(origin + new Vector2(x, y) * gridScale, gridScale * 0.1f);
                }
            }
        }

        public void TempDrawExits()
        {

        }
    }
    #endregion

    #region Enemy Pathfinding
    List<Node> nodeQueue = new List<Node>();
    List<Node> triedNodes = new List<Node>();

    private Dir[] GetPathAStar(Vector2 startPos, Vector2 goal)
    {
        bool[,] env = new bool[1, 1];

        Vector2 origin;
        Vector2Int nodeStart = Vector2Int.zero;
        Vector2Int nodeGoal = Vector2Int.zero;
        foreach (Chunk c in chunks)
        {
            if (startPos.y < c.holder.position.y)
                continue;
            else
            {
                env = c.nodes;
                origin = c.holder.position;

                nodeStart = ((startPos - origin) / gridScale).FloorToV2Int();

                //If the goal is below this chunk, make the connectNode the target;
                if (goal.y < c.holder.position.y)
                {
                    nodeGoal = c.bottomConnectNode;
                    if (nodeStart == nodeGoal)
                    {
                        Dir[] connection = new Dir[1];
                        connection[0] = Dir.bottom;
                        return connection;
                    }
                }
                else if (goal.y > c.holder.position.y + Chunk.Height * SimpleTile.heightInCells * gridScale)
                {
                    nodeGoal = c.topConnectNode;
                    if (nodeStart == nodeGoal)
                    {
                        Dir[] connection = new Dir[1];
                        connection[0] = Dir.top;
                        return connection;
                    }
                }
                else
                    nodeGoal = ((goal - origin) / gridScale).FloorToV2Int();

                break;
            }
        }

        bool foundGoal = false;
        int nodeIndex = 0;
        nodeQueue.Clear();
        nodeQueue.Add(new Node(nodeStart, Dir.bottom, 0, Cost(nodeStart, nodeGoal), -1));
        triedNodes.Clear();
        
        int counter = 0;
        while (nodeQueue.Count > 0 && foundGoal == false && counter < 1000)
        {
            counter += 1;
            Node currentNode = nodeQueue[0];
            //Remove the first node from the list (this node).
            triedNodes.Add(currentNode);
            nodeIndex = triedNodes.Count - 1;
            nodeQueue.RemoveAt(0);

            for (int dir = 0; dir < 4; ++dir)
            {
                Vector2Int offset = ((Dir)dir).ToOffset();
                offset.x += currentNode.pos.x;
                offset.y += currentNode.pos.y;

                //Debug.Log("Searching neighbor " + offset);
                if (offset == nodeGoal)
                {
                    foundGoal = true;
                    triedNodes.Add(new Node(offset, (Dir) dir, currentNode.stepsFromStart +1, Cost(offset, nodeGoal), nodeIndex));
                    break;
                }

                if (offset.x >= env.GetLength(0) || offset.x < 0 ||
                    offset.y >= env.GetLength(1) || offset.y < 0)
                    continue;

                if (env[offset.x, offset.y] == true)
                {
                    if (nodeQueue.Find(X => X.pos == offset) == null &&
                        triedNodes.Find(X => X.pos == offset) == null)
                    {
                        Node newNode = new Node(offset, (Dir)dir, currentNode.stepsFromStart + 1, Cost(offset, nodeGoal), nodeIndex);
                        int index = nodeQueue.Count - 1;
                        while (index > 0 && newNode.H < nodeQueue[index].H)
                            --index;
                        nodeQueue.Insert(index + 1, newNode);
                        //nodeQueue.Add(new Node(offset, (Dir)dir, currentNode.stepsFromStart + 1, Cost(offset, nodeGoal), nodeIndex));
                    }
                }
            }
        }

        List<Dir> path = new List<Dir>();
        if (foundGoal)
        {
            Node nextNode = triedNodes[triedNodes.Count - 1];
            while (nextNode.parentIndex != -1)
            {
                path.Insert(0, nextNode.action);
                nextNode = triedNodes[nextNode.parentIndex];
            }
        }

        return path.ToArray();
    }

    private int Cost(Vector2Int from, Vector2Int to)
    {
        return Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);
    }

    private class Node
    {
        public Vector2Int pos = Vector2Int.zero;
        public Dir action = Dir.top;
        public int stepsFromStart = -1;
        public int minStepsToEnd = -1;
        public int parentIndex = -1;

        public Node(Vector2Int Position, Dir Action, int Level, int Cost, int ParentIndex)
        {
            pos = Position;
            action = Action;
            stepsFromStart = Level;
            minStepsToEnd = Cost;
            parentIndex = ParentIndex;
        }

        public int H
        {
            get { return stepsFromStart + minStepsToEnd; }
        }
    }

    private Dir[] dirPath;
    private Vector2 pathStart;
    private void DrawPlayerAndMouseNodes()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 origin = Vector2Int.zero;
        Vector2Int nodeStart = Vector2Int.zero;
        Vector2Int nodeGoal = Vector2Int.zero;
        foreach (Chunk c in chunks)
        {
            if (player.transform.position.y < c.holder.position.y)
                continue;
            else
            {
                origin = c.holder.position;
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(origin, gridScale * 0.4f);

                nodeStart = (((Vector2)player.transform.position - origin) / gridScale).FloorToV2Int();

                //If the goal is below this chunk, make the connectNode the target;
                if (mousePos.y < c.holder.position.y)
                    nodeGoal = c.bottomConnectNode;
                else if (mousePos.y > c.holder.position.y + Chunk.Height * SimpleTile.heightInCells * gridScale)
                    nodeGoal = c.topConnectNode;
                else
                    nodeGoal = ((mousePos - origin) / gridScale).FloorToV2Int();
                break;
            }
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(player.transform.position, gridScale * 0.5f);
        Gizmos.DrawSphere(origin + (Vector2)nodeStart * gridScale + Vector2.one * gridScale * 0.5f, gridScale * 0.4f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(mousePos, gridScale * 0.5f);
        Gizmos.DrawSphere(origin + (Vector2)nodeGoal * gridScale + Vector2.one * gridScale * 0.5f, gridScale * 0.4f);

        if (dirPath != null && dirPath.Length > 0)
        {
            Vector2 lastPoint = pathStart;
            Vector2 nextPoint = pathStart;
            foreach (Dir dir in dirPath)
            {
                nextPoint = lastPoint + dir.ToV2() * gridScale;
                Gizmos.DrawLine(lastPoint, nextPoint);
                lastPoint = nextPoint;
            }
        }
    }
    private void DrawBrokenPath()
    {
        if (chunks.Count < 1 || pathIsBroken == false)
            return;
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 origin = Vector2Int.zero;
        origin = chunks[chunks.Count - 1].holder.position;
        Vector2 tileSize = new Vector2(SimpleTile.widthInCells * gridScale, SimpleTile.heightInCells * gridScale);

        foreach (Vector2Int tile in brokenPathVals)
        {
            Gizmos.DrawSphere(origin + (Vector2)tile * tileSize + tileSize * 0.5f, gridScale * 0.4f);
        }
    }
    #endregion

    #region Camera
    private Vector2 unroundedCamPos = Vector2.zero;

    public static Vector2 RoundToNearestPixel(Vector2 pos, Camera viewingCamera)
    {
        float x = (Screen.height / (viewingCamera.orthographicSize * 2)) * pos.x;
        x = Mathf.Round(x);
        float adjustedX = x / (Screen.height / (viewingCamera.orthographicSize * 2));

        float y = (Screen.height / (viewingCamera.orthographicSize * 2)) * pos.y;
        y = Mathf.Round(y);
        float adjustedY = y / (Screen.height / (viewingCamera.orthographicSize * 2));
        return new Vector2(adjustedX, adjustedY);
    }

    private void MoveCameraToPlayer()
    {
        float distToPlayer = Vector2.Distance(cameraTansform.position, player.transform.position);
        float targetSpeed = cameraSpeedCurve.Evaluate(distToPlayer);
        unroundedCamPos = Vector2.MoveTowards(unroundedCamPos, player.transform.position, targetSpeed * Time.deltaTime);
        //cameraTansform.position = RoundToNearestPixel(unroundedCamPos, Camera.main);
        cameraTansform.position = unroundedCamPos;
    }
    #endregion

    #region Steady Downwards Camera [Depreciated]
    private float cameraFallSpeed = 1;
    private float speedNearBottomFactor = 2;
    private float speedIncreaseRate = 0.01f;
    private float levelBottom = 5;
    private float levelSide = 3;
    private float deathHeight = 7;
    private void MoveCameraDown()
    {
        if (playerDead || player.transform.position.y > cameraTansform.position.y + deathHeight)
        {
            if (!playerDead)
            {
                Debug.Log("Player is dead!!!");
                playerDead = true;
            }
            return;
        }

        //If player near bottom go down faster.
        float distFromBottom = player.transform.position.y - (cameraTansform.position.y - levelBottom);
        float speedFactor = 1 + (levelBottom - distFromBottom) / levelBottom;
        //Debug.Log("Dist = " + distFromBottom + ", factor = " + speedFactor);
        cameraTansform.position += new Vector3(0, -cameraFallSpeed * speedFactor * Time.deltaTime);

        //if (cameraTansform.position.y <= (-_nextY + startNumRows) * blockSize)
        //    GenerateRow();

        cameraFallSpeed += speedIncreaseRate * Time.deltaTime;
    }
    #endregion
}

public enum Dir
{
    top,
    right,
    bottom,
    left,
}

public static class Utility
{
    public static bool Outside(this int val, int bound1, int bound2)
    {
        if (bound1 < bound2)
            return (val < bound1 || val > bound2);
        else
            return (val > bound1 || val < bound2);
    }

    public static Dir Opposite(this Dir dir)
    {
        return (Dir)(((int)dir + 2) % 4);
    }

    public static int OppositeIfDir(this int dir)
    {
        return (dir + 2) % 4;
    }

    public static Vector2Int ToOffset(this Dir dir)
    {
        if (dir == Dir.right)
            return Vector2Int.right;
        else if (dir == Dir.bottom)
            return Vector2Int.down;
        else if (dir == Dir.left)
            return Vector2Int.left;
        else// if(dir == Dir.top)
            return Vector2Int.up;
    }

    public static Vector2 ToV2(this Dir dir)
    {
        return dir.ToOffset();
    }

    public static Vector3 MultipliedBy(this Vector3 a, Vector3 b)
    {
        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }

    public static Vector2 MultipliedBy(this Vector2 a, Vector2 b)
    {
        return new Vector2(a.x * b.x, a.y * b.y);
    }

    public static Vector2Int FloorToV2Int(this Vector2 a)
    {
        return new Vector2Int(Mathf.FloorToInt(a.x), Mathf.FloorToInt(a.y));
    }
}
