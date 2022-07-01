using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    #region Settings and References
    public static bool holdToMoveMode = true;

    [Header("References")]
    public Transform cameraTansform;
    public GridPlayer player;
    public Enemy testEnemy;
    public GameObject enemyPrefab;
    public GameObject recordPre;
    public Text scoreText;
    public RectTransform introScreen;
    public RectTransform gameOverScreen;
    public Text gameOverText;
    public AudioClip recordScratch;
    public string tilesPath = "/Prefabs/Tiles/";

    [Header("Settings")]
    public AnimationCurve cameraSpeedCurve = new AnimationCurve();
    [Min(3)]
    public int maxChunks = 3;
    public LayerMask wallLayers;
    public bool startInMiddle = false;
    public bool giveTempoScore = false;
    public float enemySize = 2f;

    [Header("Music Settings")]
    public float musicBeatRate = 0.245942f;
    public float firstBeatTime = 0.22f;
    public int beatsBeforeEnemyMoves = 3;
    public float musicDelay = 0.5f;
    public int beatsPerPlayerMove = 2;
    public int beatsPerEnemyMove = 4;
    [Tooltip("Example: If you have 4 beats per enemy move, then: TRUE = enemy will move on 2, 3, and 4. FALSE = enemy will move on 1 only.")]
    public bool enemyMovesOnOffBeats = true;

    [Header("Enemy Spawning")]
    public List<int> NumChunksForNewEnemies = new List<int>();
    public bool SpawnBeatEnemiesAtTop = true;
    public List<int> NumBeatsForNewEnemies = new List<int>();

    public const float gridScale = 0.5f;
    [Range(0.01f, 0.5f)]
    private const float relativeNodeRadius = 0.3f;
    private const float minNumCellsForMove = 0.3f; //Number of cells from the center of the screen before a move will not count.
    public float wakeUpRadius = 0.3f;

    [Header("Debug Controls")]
    public List<GameObject> tilePrefabs = new List<GameObject>();
    public bool showPathfindingGizmos = false;
    public bool showBeatsAnalysis = false;
    public float beatsXScale = 3f;
    public float beatsYScale = 10f;
    public float beatsSize = 0.5f;
    public Vector2 beatsOrigin = new Vector2();

    public List<float> beatTimes = new List<float>();
    #endregion

    #region Controller Variables
    private List<SimpleTile>[] sortedTiles = new List<SimpleTile>[4];

    private List<GameObject> zeroConnectionTiles = new List<GameObject>();

    private bool gameStarted = false;
    private bool gameOver = false;
    private float nextBeat = 0;
    private int nextManualBeat = 0;
    private int numBeats = 0;
    private AudioSource musicPlayer;

    private List<Vector2> downConnections = new List<Vector2>();

    private List<Enemy> enemies = new List<Enemy>();
    private List<Enemy> idleEnemies = new List<Enemy>();

    public float score = 0;
    #endregion

    #region Unity Events
    // Start is called before the first frame update
    void Start()
    {
        gameOverScreen.gameObject.SetActive(false);
        SortTilePrefabs();
        //Sort num beats/chunks per enemy //NEEDS IMPROVEMENT HERE
        //NumBeatsForNewEnemies.Sort(IComparer<int>)
        musicPlayer = GetComponent<AudioSource>();

        GenerateStartingTiles();

        player.transform.position = chunks[0].holder.position + Vector3.right * (chunks[0].endSlot.x + 0.5f) * SimpleTile.widthInCells * gridScale;
        player.transform.position += Vector3.up * gridScale * 0.5f;
        if (SimpleTile.widthInCells % 2 == 0)
            player.transform.position += Vector3.right * gridScale * 0.5f;
        
        //Place the camera two tiles above the player so it can move and imply the motion
        unroundedCamPos = player.transform.position + Vector3.up * SimpleTile.heightInCells * gridScale * 2f;
        cameraTansform.position = unroundedCamPos;

        Vector2 enemyPos = player.transform.position + (Vector3.up * gridScale * SimpleTile.heightInCells * 2);
        CreateEnemy(enemyPos);
    }

    // Update is called once per frame
    void Update()
    {
        if (!gameStarted)
        {
            if (Input.GetMouseButtonDown(0))
            {
                introScreen.gameObject.SetActive(false);
                gameStarted = true;
                musicPlayer.Play();
                nextBeat = Time.time + firstBeatTime;
                numBeats = -beatsBeforeEnemyMoves;
            }
            return;
        }

        if (gameOver)
            return;

        if (!musicPlayer.isPlaying)
        {
            Debug.Log("Sound Finished");
            GameOver(true);
        }

        if (Time.time >= nextBeat)
        {
            nextBeat += musicBeatRate;
            numBeats += 1;
            if (holdToMoveMode && beatsPerPlayerMove != 0 && numBeats % beatsPerPlayerMove == 0)
                PlayerHoldToMove();

            if (player.transform.position.y < chunks[chunks.Count - 2].holder.position.y)
                GenerateTileAndEnemies();

            if (NumBeatsForNewEnemies.Count > 0)
            {
                int nextEnemySpawn = NumBeatsForNewEnemies[0];

                if (numBeats >= nextEnemySpawn)
                {
                    NumBeatsForNewEnemies.RemoveAt(0);
                    if (SpawnBeatEnemiesAtTop)
                        CreateEnemy((Vector2)chunks[1].topConnectNode * gridScale + (Vector2)chunks[chunks.Count - 1].holder.position);
                    else
                        CreateEnemy((Vector2)chunks[1].bottomConnectNode * gridScale + (Vector2)chunks[chunks.Count - 1].holder.position);
                    //CreateEnemy(chunks[0].holder.position); //NEEDS IMPROVEMENT HERE
                }
            }

            if (enemyMovesOnOffBeats == true)
            {
                //Move on off-beats (all beats except first)
                if (numBeats > 0 && beatsPerEnemyMove != 0 && numBeats % beatsPerEnemyMove != 0)
                    MoveEnemies();
                else
                    IdleEnemies();
            }
            else
            {
                //Move on main-beats (only first in set)
                if (numBeats > 0 && beatsPerEnemyMove != 0 && numBeats % beatsPerEnemyMove == 0)
                    MoveEnemies();
                else
                    IdleEnemies();
            }
        }

        MoveCameraToPlayer();

        if (!holdToMoveMode)
            PlayerTapToMove();
    }

    private void OnDrawGizmos()
    {
        if (showBeatsAnalysis)
            AnalyzeBeats();

        if (showPathfindingGizmos)
        {
            foreach (Chunk c in chunks)
            {
                TempDrawNodesForChunk(c);
                //c.TempDrawNodes();
            }
            DrawPlayerAndMouseNodes();
        }
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
    private void PlayerTapToMove()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 delta = mousePos - player.transform.position;
            
            if (DirectionTooCloseToCenter(delta))
                return;

            Dir move = DeltaToDir(delta);

            if (MovePlayer(move))
            {
                if (giveTempoScore)
                {
                    //Score the move
                    float diff = nextBeat - musicPlayer.time;
                    float moveScore = ((musicBeatRate / 5) - Mathf.PingPong(diff, musicBeatRate / 2)) / (musicBeatRate / 5);
                    moveScore = Mathf.Floor(moveScore * 100);
                    score += moveScore;
                    scoreText.text = score.ToString();
                }
            }
        }
    }

    private void PlayerHoldToMove()
    {
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 delta = mousePos - player.transform.position;

            if (!DirectionTooCloseToCenter(delta))
            {
                Dir move = DeltaToDir(delta);
                MovePlayer(move);
                return;
            }
        }

        player.Idle();
    }

    private bool MovePlayer(Dir movement)
    {
        if (NoCollisionAtPoint(player.transform.position + movement.ToV3() * gridScale))
            player.Move(movement);
        else
            return false;
        
        if (player.CheckForRecord())
        {
            score += 100;
            scoreText.text = score.ToString();
        }

        return true;
    }

    private bool NoCollisionAtPoint(Vector3 pos)
    {
        return !Physics2D.OverlapCircle(pos, gridScale * relativeNodeRadius, wallLayers);
    }

    private bool DirectionTooCloseToCenter(Vector2 delta)
    {
        if (delta.sqrMagnitude > Mathf.Pow(gridScale * minNumCellsForMove, 2))
            return false;

        return true;
    }

    private Vector2 DeltaToMove(Vector2 delta)
    {
        Vector2 move = Vector2.zero;
        
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
        return move;
    }

    private Dir DeltaToDir(Vector2 delta)
    {
        Dir move = Dir.top;

        if (delta.x > delta.y) //Right or Down
        {
            if (delta.x > -delta.y) //Right
                move = Dir.right;
            else //Down
                move = Dir.bottom;
        }
        else //Up or Left
        {
            if (delta.x > -delta.y) //Up
                move = Dir.top;
            else //Left
                move = Dir.left;
        }
        return move;
    }

    private void CreateEnemy(Vector2 position)
    {
        GameObject GO = Instantiate(enemyPrefab, position, Quaternion.identity);
        Enemy newEnemy = GO.GetComponent<Enemy>();
        enemies.Add(newEnemy);
    }

    private void CreateIdleEnemy(Vector2 position)
    {
        GameObject GO = Instantiate(enemyPrefab, position, Quaternion.identity);
        Enemy newEnemy = GO.GetComponent<Enemy>();
        idleEnemies.Add(newEnemy);
    }

    private void MoveEnemies()
    {
        foreach (Enemy E in enemies)
        {
            E.path = GetPathAStar(E.transform.position, player.transform.position);
            E.Move(player.transform.position);
            if (Vector2.Distance(E.transform.position, player.transform.position) < gridScale * enemySize)
                GameOver(false);
        }
        for (int i = 0; i < idleEnemies.Count; ++i)
        {
            if (idleEnemies[i].transform.position.y > chunks[0].holder.position.y)
            {
                Destroy(idleEnemies[i].gameObject);
                idleEnemies.RemoveAt(i);
                --i;
            }
            else if (Vector2.Distance(idleEnemies[i].transform.position, player.transform.position) < wakeUpRadius)
            {
                enemies.Add(idleEnemies[i]);
                idleEnemies.RemoveAt(i);
                --i;
            }
        }
    }

    private void IdleEnemies()
    {
        foreach (Enemy E in enemies)
        {
            E.Idle();
        }
    }

    private void GameOver(bool goodEnding)
    {
        gameOver = true;
        gameOverScreen.gameObject.SetActive(true);

        if (goodEnding)
        {
            gameOverText.text = "You survived for the whole song!!!";
            score += 1000;
            musicPlayer.volume = musicPlayer.volume * 0.4f;
        }
        else
        {
            //gameOverText.text = "You got caught!";
            musicPlayer.clip = recordScratch;
            musicPlayer.loop = false;
        }
        musicPlayer.Play();
    }

    public void BackToMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
    #endregion

    #region Tiles and Chunks
    private void GenerateTileAndEnemies()
    {
        GenerateTileChunk();

        if (NumChunksForNewEnemies.Count > 0)
        {
            int nextChunkSpawn = NumChunksForNewEnemies[0];

            if (numChunks >= nextChunkSpawn)
            {
                NumChunksForNewEnemies.RemoveAt(0);
                CreateEnemy((Vector2)chunks[chunks.Count - 1].bottomConnectNode * gridScale + (Vector2)chunks[chunks.Count - 1].holder.position); //NEEDS IMPROVEMENT HERE
            }
        }
    }

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
            bool hasAConnection = false;
            for (int i = 0; i < 4; ++i)
            {
                if (tile.connections[i])
                {
                    sortedTiles[i].Add(tile);
                    hasAConnection = true;
                }
            }

            if (hasAConnection == false)
                zeroConnectionTiles.Add(pre);
        }

        //Add the first loaded tile if there are no zero-connection tiles (used for a default)
        if (zeroConnectionTiles.Count == 0)
            zeroConnectionTiles.Add(tilePrefabs[0]);
    }
    
    private void GenerateStartingTiles()
    {
        CreateCapChunk();
        GenerateTileChunk();
        GenerateTileChunk();
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

        thisChunk.slots = new SimpleTile[Chunk.Width, 3];

        bool[,][] connectionsMatrix = new bool[Chunk.Width, 3][];
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

        for (int x = 0; x < Chunk.Width; ++x)
            connectionsMatrix[x, 1] = new bool[4];

        for (int x = 0; x < Chunk.Width; ++x)
            connectionsMatrix[x, 2] = new bool[4];

        connectionsMatrix[startingTile, 0][(int)Dir.top] = true;
        connectionsMatrix[startingTile, 1][(int)Dir.bottom] = true;
        connectionsMatrix[startingTile, 1][(int)Dir.top] = true;
        connectionsMatrix[startingTile, 2][(int)Dir.bottom] = true;

        FillChunkWithTilesAndEnemies(thisChunk, connectionsMatrix, 0);

        if (zeroConnectionTiles.Count > 0)
        {
            GameObject blankTile = zeroConnectionTiles[0];

            Vector2 tileSize = new Vector2(SimpleTile.widthInCells, SimpleTile.heightInCells) * gridScale;
            Vector2 halfTile = new Vector2(SimpleTile.widthInCells * gridScale * 0.5f, SimpleTile.heightInCells * gridScale * 0.5f);
            for (int y = 0 - Chunk.Height * maxChunks; y < Chunk.Height; ++y)
            {
                //Create two tiles on the left and two on the right
                Vector2 tilePos = (new Vector2(-1, y)).MultipliedBy(tileSize) + (Vector2)thisChunk.holder.position + halfTile;
                Instantiate(blankTile, tilePos, Quaternion.identity, thisChunk.holder).GetComponent<SimpleTile>();

                tilePos = (new Vector2(Chunk.Width, y)).MultipliedBy(tileSize) + (Vector2)thisChunk.holder.position + halfTile;
                Instantiate(blankTile, tilePos, Quaternion.identity, thisChunk.holder).GetComponent<SimpleTile>();

                GenerateNodesForChunk(thisChunk);
            }
        }

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

            for (int dir = 0; dir < 4; ++dir)
            {
                if (connectionsMatrix[currentTile.x, currentTile.y][dir] == false)
                {
                    offset = currentTile + ((Dir)dir).ToOffset();
                    if (offset.x < Chunk.Width && offset.x >= 0 &&
                        offset.y < Chunk.Height && offset.y >= 0)
                        connectionsMatrix[offset.x, offset.y][dir.OppositeDir()] = false;
                }
            }

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
                        else if (connectionsMatrix[offset.x, offset.y][dir.OppositeDir()])
                            neededDirections[dir] = true;
                    }
                    connectionsMatrix[x, y] = neededDirections;
                    filledTiles[x, y] = true;
                }
            }
        }
        #endregion

        FillChunkWithTilesAndEnemies(thisChunk, connectionsMatrix, Mathf.Min(numChunks, 3));

        GenerateNodesForChunk(thisChunk);

        int numRecords = 0;
        bool[,] filledNodes = new bool[SimpleTile.widthInCells * Chunk.Width, SimpleTile.heightInCells * Chunk.Height];
        for (int i = 0; i < Chunk.Width * Chunk.Height * 5 + (numChunks * 10); ++i)
        {
            Vector2Int randomPos = new Vector2Int(Random.Range(0, SimpleTile.widthInCells * Chunk.Width), Random.Range(0, SimpleTile.heightInCells * Chunk.Height));
            if (filledNodes[randomPos.x, randomPos.y] == false && thisChunk.nodes[randomPos.x, randomPos.y] == true)
            {
                numRecords += 1;
                filledNodes[randomPos.x, randomPos.y] = true;
                Instantiate(recordPre, (Vector2)thisChunk.holder.position + (Vector2)randomPos * gridScale + Vector2.one * gridScale * 0.5f, Quaternion.identity, thisChunk.holder);
                if (numRecords >= Chunk.Width * Chunk.Height)
                    break;
            }
        }

        chunks.Add(thisChunk);

        if (chunks.Count > maxChunks + 1)
        {
            Destroy(chunks[1].holder.gameObject, 0.01f);
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
        List<Dir> path = new List<Dir>();
        bool[,] env = new bool[Chunk.Width, Chunk.Height];

        Vector2Int currentTile = startSlot;
        env[currentTile.x, currentTile.y] = true;

        List<Dir> options = new List<Dir>();
        List<Vector2Int> nodes = new List<Vector2Int>();

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
                    env[currentTile.x, currentTile.y] = true;
                    break;
                }
                else
                    options.RemoveAt(choice);
            }
        }

        //Return the path.

        return path.ToArray();
    }

    private void FillChunkWithTilesAndEnemies(Chunk thisChunk, bool[,][] connections, int numEnemies)
    {
        Vector2 tileSize = new Vector2(SimpleTile.widthInCells, SimpleTile.heightInCells) * gridScale;
        Vector2 halfTile = new Vector2(SimpleTile.widthInCells * gridScale * 0.5f, SimpleTile.heightInCells * gridScale * 0.5f);

        List<Vector2> deadEndTiles = new List<Vector2>();

        for (int x = 0; x < connections.GetLength(0); ++x)
        {
            for (int y = 0; y < connections.GetLength(1); ++y)
            {
                GameObject chosenTile = FindMatchingTile(connections[x,y]);

                //Instantiate the chosen tile and add to the chunk
                Vector2 tilePos = (new Vector2(x, y)).MultipliedBy(tileSize) + (Vector2)thisChunk.holder.position + halfTile;

                SimpleTile newTile = Instantiate(chosenTile, tilePos, Quaternion.identity, thisChunk.holder).GetComponent<SimpleTile>();
                thisChunk.slots[x, y] = newTile;

                int numEntrances = 0;
                for (int dir = 0; dir < 4; ++dir)
                {
                    if (connections[x,y][dir])
                        ++numEntrances;
                }

                if (numEntrances == 1)
                {
                    deadEndTiles.Add(tilePos);
                }
            }
        }

        for (int i = 0; i < numEnemies; ++i)
        {
            if (deadEndTiles.Count > 0)
            {
                int randomOption = Random.Range(0, deadEndTiles.Count);
                CreateIdleEnemy(deadEndTiles[randomOption]);
                deadEndTiles.RemoveAt(randomOption);
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

        int randomIndex = Random.Range(0, zeroConnectionTiles.Count);
        GameObject chosenTile = zeroConnectionTiles[randomIndex];
        if (entrance == -1 || entrance > sortedTiles[entrance].Count)
            return chosenTile;

        bool foundPerfectTile = false;
        List<GameObject> perfectOptions = new List<GameObject>();
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
            {
                perfectOptions.Add(tilePre.gameObject);
                foundPerfectTile = true;
            }
        }
        if (foundPerfectTile)
        {
            randomIndex = Random.Range(0, perfectOptions.Count);
            return perfectOptions[randomIndex];
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

        public void TempDrawExits()
        {

        }
    }

    public void TempDrawNodesForChunk(Chunk chunk)
    {
        Gizmos.color = Color.red;
        Vector2 origin = (Vector2)chunk.holder.position + Vector2.one * gridScale * 0.5f;
        for (int x = 0; x < chunk.nodes.GetLength(0); ++x)
        {
            for (int y = 0; y < chunk.nodes.GetLength(1); ++y)
            {
                if (chunk.nodes[x, y])
                {
                    /*
                    if (HasPathAStar(chunk, new Vector2Int(x, y)))
                        Gizmos.color = Color.red;
                    else
                        Gizmos.color = Color.yellow;
                    */
                    Gizmos.DrawSphere(origin + new Vector2(x, y) * gridScale, gridScale * 0.1f);
                }
            }
        }
    }

    public void GenerateNodesForChunk(Chunk chunk)
    {
        chunk.nodes = new bool[chunk.slots.GetLength(0) * SimpleTile.widthInCells, chunk.slots.GetLength(1) * SimpleTile.heightInCells];

        Vector2 origin = (Vector2)chunk.holder.position + Vector2.one * gridScale * 0.5f;
        for (int y = 0; y < chunk.nodes.GetLength(1); ++y)
        {
            for (int x = 0; x < chunk.nodes.GetLength(0); ++x)
            {
                if (NoCollisionAtPoint(origin + new Vector2(x, y) * gridScale))
                {
                    chunk.nodes[x, y] = true;
                }
            }
        }

        chunk.bottomConnectNode = new Vector2Int(Mathf.FloorToInt((chunk.endSlot.x + 0.5f) * SimpleTile.widthInCells), 0);
        chunk.topConnectNode = new Vector2Int(Mathf.FloorToInt((chunk.startSlot.x + 0.5f) * SimpleTile.widthInCells), chunk.nodes.GetLength(1));
    }
    #endregion

    #region Enemy Pathfinding
    List<Node> nodeQueue = new List<Node>();
    List<Node> triedNodes = new List<Node>();

    private Dir[] GetPathAStar(Vector2 startPos, Vector2 goal)
    {
        bool[,] env = new bool[Chunk.Width * SimpleTile.widthInCells, Chunk.Height * SimpleTile.heightInCells];

        Vector2 origin;
        Vector2Int nodeStart = Vector2Int.zero;
        Vector2Int nodeGoal = Vector2Int.zero;
        foreach (Chunk c in chunks)
        {
            if (startPos.y < c.holder.position.y)
                continue;
            else
            {
                for (int y = 0; y < c.nodes.GetLength(1); ++y)
                {
                    for (int x = 0; x < c.nodes.GetLength(0); ++x)
                    {
                        env[x, y] = c.nodes[x, y];
                    }
                }
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

                //For each enemy, check if it is in the chunk, then change the node it is on to blocked (false) so enemies take different paths to each other
                foreach (Enemy E in enemies)
                {
                    if (E.transform.position.y >= c.holder.position.y &&
                        E.transform.position.y <= c.holder.position.y + Chunk.Height * SimpleTile.heightInCells * gridScale)
                    {
                        Vector2Int enemyNode = (((Vector2)E.transform.position - origin) / gridScale).FloorToV2Int();
                        if (enemyNode.x >= env.GetLength(0) || enemyNode.x < 0 ||
                            enemyNode.y >= env.GetLength(1) || enemyNode.y < 0)
                            continue;
                        env[enemyNode.x, enemyNode.y] = false;
                    }
                }

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

    private bool HasPathAStar(Chunk c, Vector2 goalPos)
    {
        Vector2Int goalNode = Vector2Int.zero;
        if (goalPos.y < c.holder.position.y)
            return false;
        else if (goalPos.y > c.holder.position.y + Chunk.Height * SimpleTile.heightInCells * gridScale)
            return false;
        else
            goalNode = ((goalPos - (Vector2)c.holder.position) / gridScale).FloorToV2Int();

        return HasPathAStar(c, goalNode);
    }

    private bool HasPathAStar(Chunk c, Vector2Int goalNode)
    {
        bool[,] env = c.nodes;
        bool foundGoal = false;
        int nodeIndex = 0;
        nodeQueue.Clear();
        nodeQueue.Add(new Node(c.topConnectNode, Dir.bottom, 0, Cost(c.topConnectNode, goalNode), -1));
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

                if (offset == goalNode)
                    return true;

                if (offset.x >= env.GetLength(0) || offset.x < 0 ||
                    offset.y >= env.GetLength(1) || offset.y < 0)
                    continue;

                if (env[offset.x, offset.y] == true)
                {
                    if (nodeQueue.Find(X => X.pos == offset) == null &&
                        triedNodes.Find(X => X.pos == offset) == null)
                    {
                        Node newNode = new Node(offset, (Dir)dir, currentNode.stepsFromStart + 1, Cost(offset, goalNode), nodeIndex);
                        int index = nodeQueue.Count - 1;
                        while (index > 0 && newNode.H < nodeQueue[index].H)
                            --index;
                        nodeQueue.Insert(index + 1, newNode);
                    }
                }
            }
        }

        return false;
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
        cameraTansform.position = RoundToNearestPixel(unroundedCamPos, Camera.main);
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

    public static int OppositeDir(this int dir)
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

    public static Vector3 ToV3(this Dir dir)
    {
        return dir.ToV2();
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
