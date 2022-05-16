using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller : MonoBehaviour
{
    #region Settings and References
    public Transform cameraTansform;
    public Transform player;
    public AnimationCurve cameraSpeedCurve = new AnimationCurve();
    public float musicBeatRate = 0.25f;
    public float firstBeat = 0.5f;
    public int beatsPerAction = 4;
    public float gridScale = 0.5f;

    public GameObject defaultTile;

    public List<GameObject> tilePrefabs = new List<GameObject>();
    #endregion

    #region Controller Variables
    private List<SimpleTile>[] sortedTiles = new List<SimpleTile>[4];
    
    private bool playerDead = false;
    private float nextBeat = 0;
    private int numBeats = 0;
    private AudioSource musicPlayer;

    private List<Tile[]> generatedTiles = new List<Tile[]>();
    private List<Vector2> downConnections = new List<Vector2>();
    #endregion

    #region Unity Events
    // Start is called before the first frame update
    void Start()
    {
        SortTilePrefabs();
        GenerateStartingTiles();
        musicPlayer = GetComponent<AudioSource>();
        nextBeat = firstBeat;
    }

    // Update is called once per frame
    void Update()
    {
        if (!musicPlayer.isPlaying && Time.time >= firstBeat)
            musicPlayer.Play();

        if (Time.time >= nextBeat)
        {
            nextBeat += musicBeatRate;
            numBeats += 1;
            if (beatsPerAction != 0 && numBeats % beatsPerAction == 0)
            {
                Vector3 movement = Vector3.zero;
                if (Input.GetKey(KeyCode.W))
                    movement += new Vector3(0, gridScale);
                else if (Input.GetKey(KeyCode.S))
                    movement += new Vector3(0, -gridScale);
                else if (Input.GetKey(KeyCode.D))
                    movement += new Vector3(gridScale, 0);
                else if (Input.GetKey(KeyCode.A))
                    movement += new Vector3(-gridScale, 0);

                if (!Physics2D.OverlapPoint(player.position + movement))
                    player.position += movement;
            }
        }
        MoveCameraToPlayer();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(cameraTansform.position - new Vector3(0, levelBottom), Vector3.forward + Vector3.right);
        Gizmos.DrawWireCube(cameraTansform.position + new Vector3(levelSide, 0), Vector3.forward + Vector3.up);
        Gizmos.DrawWireCube(cameraTansform.position - new Vector3(levelSide, 0), Vector3.forward + Vector3.up);
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
        GenerateTileChunk();
        GenerateTileChunk();

        player.position = chunks[0].slots[chunks[0].startSlot.x, chunks[0].startSlot.y].transform.position;
    }

    private void GenerateTileChunk()
    {
        Vector2 chunkSize = new Vector2(SimpleTile.widthInCells, SimpleTile.heightInCells) * SimpleTile.cellSize;
        Vector2 chunkPos = new Vector2(0, -1 * Chunk.Height * SimpleTile.heightInCells * SimpleTile.cellSize * numChunks);
        Chunk thisChunk = new Chunk(transform, chunkPos, ++numChunks);

        //Get the end point of the previous chunk
        //Generate a random path from that point to a random point at the bottom of this chunk
        // Iterate through the path and Get random tiles with appropriate connections to fill the slots
        //Loop through the slots and try to fill empty ones with a tile that matches the connections

        thisChunk.startSlot = new Vector2Int(0, Chunk.Height - 1);
        bool firstChunk = true;
        if (chunks.Count > 0)
        {
            firstChunk = false;
            thisChunk.startSlot.x = chunks[chunks.Count - 1].endSlot.x;
        }
        else
            thisChunk.startSlot.x = Chunk.Width / 2;

        //Debug.Log("StartSlot = " + startSlot);

        thisChunk.endSlot = new Vector2Int(Random.Range(0, Chunk.Width), 0);
        //Debug.Log("EndSlot = " + thisChunk.endSlot);

        //Generate a path between these two by adding random directions that still have a path to the end point.
        List<Dir> testPath = new List<Dir>();
        int x = thisChunk.startSlot.x;
        int y = thisChunk.startSlot.y;

        //Temp path that just makes the simplest L shape to the end
        while (x != thisChunk.endSlot.x)
        {
            if (x > 100 || x < -100)
                break;
            if (x < thisChunk.endSlot.x)
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
        while (y != thisChunk.endSlot.y)
        {
            if (y > 100 || y < -100)
                break;
            if (y > thisChunk.endSlot.y)
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

        //Loop through the path of Dirs
        //Search the tile list of the (opposite of the) previous direction for a tile with the next direction (uses down for first and last direction)
        //Create that tile and add it to the chunk
        Debug.Log("Path count = " + testPath.Count);
        Vector2Int currentTile = thisChunk.startSlot; //used to put the tile in the correct slot
        for (int i = 0; i <= testPath.Count; ++i)
        {
            //Define required connections
            Dir entrance = Dir.top;
            Dir exit = Dir.bottom;
            if (i != 0)
                entrance = testPath[i - 1].Opposite();

            if (i < testPath.Count)
                exit = testPath[i];
            
            bool[] requiredConns = new bool[4];
            if (!firstChunk || i != 0)
                requiredConns[(int)entrance] = true;
            requiredConns[(int)exit] = true;

            //Choose two random unnecessary connections to add to this slot (if they would connect within the chunk)
            int randomDir = Random.Range(0, 4);
            Vector2Int offset = currentTile + ((Dir)randomDir).ToOffset();
            if (offset.x < Chunk.Width && offset.x >= 0 &&
                offset.y < Chunk.Height && offset.y >= 0)
                requiredConns[randomDir] = true;

            randomDir = Random.Range(0, 4);
            offset = currentTile + ((Dir)randomDir).ToOffset();
            if (offset.x < Chunk.Width && offset.x >= 0 &&
                offset.y < Chunk.Height && offset.y >= 0)
                requiredConns[randomDir] = true;

            GameObject chosenTile = FindMatchingTile(requiredConns);

            //Instantiate the chosen tile and add to the chunk
            Vector2 tilePos = ((Vector2)currentTile).MultipliedBy(chunkSize) + chunkPos;

            SimpleTile newTile = Instantiate(chosenTile, tilePos, Quaternion.identity, thisChunk.holder).GetComponent<SimpleTile>();
            thisChunk.slots[currentTile.x, currentTile.y] = newTile;

            //Move the currentTile pointer to the next slot in the path
            currentTile += exit.ToOffset();
        }

        //Loop through all the slots in the chunk
        //If the slot is empty, find a tile that matches the required connections
        //Create the tile and add it to the chunk (so new tiles will account for it)
        for (x = 0; x < Chunk.Width; ++x)
        {
            for (y = 0; y < Chunk.Height; ++y)
            {
                if (thisChunk.slots[x, y] == null)
                {
                    //Find a chunk to fit this slot
                    bool[] neededDirections = new bool[4];
                    for (int dir = 0; dir < 4; ++dir)
                    {
                        Vector2Int offset = ((Dir)dir).ToOffset();
                        offset.x += x;
                        offset.y += y;
                        if (offset.x >= Chunk.Width || offset.x < 0 ||
                            offset.y >= Chunk.Height || offset.y < 0)
                            continue;

                        if (thisChunk.slots[offset.x, offset.y] == null)
                            neededDirections[dir] = Random.value > 0.5f;
                        else if (thisChunk.slots[offset.x, offset.y].connections[dir.OppositeIfDir()])
                            neededDirections[dir] = true;
                    }

                    GameObject chosenTile = FindMatchingTile(neededDirections);
                    Vector2 tilePos = new Vector2(x, y).MultipliedBy(chunkSize) + chunkPos;

                    SimpleTile newTile = Instantiate(chosenTile, tilePos, Quaternion.identity, thisChunk.holder).GetComponent<SimpleTile>();
                    thisChunk.slots[x, y] = newTile;
                }
            }
        }
        /*
        */

        //DeleteExtraRow();

        //_nextY += 3;
        chunks.Add(thisChunk);
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

        public Chunk(Transform parent, Vector2 pos, int number)
        {
            holder = new GameObject("Chunk " + number).transform;
            holder.SetParent(parent);
            holder.position = pos;
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
        float distToPlayer = Vector2.Distance(cameraTansform.position, player.position);
        float targetSpeed = cameraSpeedCurve.Evaluate(distToPlayer);
        unroundedCamPos = Vector2.MoveTowards(unroundedCamPos, player.position, targetSpeed * Time.deltaTime);
        cameraTansform.position = RoundToNearestPixel(unroundedCamPos, Camera.main);
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
        if (playerDead || player.position.y > cameraTansform.position.y + deathHeight)
        {
            if (!playerDead)
            {
                Debug.Log("Player is dead!!!");
                playerDead = true;
            }
            return;
        }

        //If player near bottom go down faster.
        float distFromBottom = player.position.y - (cameraTansform.position.y - levelBottom);
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
        return new Vector3(a.x * b.x, a.y * b.y);
    }
}
