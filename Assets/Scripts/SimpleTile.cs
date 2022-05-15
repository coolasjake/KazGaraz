using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleTile : MonoBehaviour
{
    public static int widthInCells = 11;
    public static int heightInCells = 7;
    public static float cellSize = 0.5f;

    [SerializeField]
    private bool showGizmos = false;

    public bool[] connections = new bool[4];

    private void OnDrawGizmos()
    {
        if (!showGizmos)
            return;

        Vector3 d = Vector3.back * 0.001f;

        Gizmos.color = Color.cyan;
        //Draw left lines
        Vector3 pos = transform.position;
        Vector3 left = d + Vector3.left * widthInCells * cellSize * 0.5f;
        Vector3 right =  d + Vector3.right * widthInCells * cellSize * 0.5f;
        Vector3 top = d + Vector3.up * heightInCells * cellSize * 0.5f;
        Vector3 bottom = d + Vector3.down * heightInCells * cellSize * 0.5f;

        //Draw border lines
        Gizmos.DrawLine(pos + left + top, pos + left + bottom);
        Gizmos.DrawLine(pos + right + top, pos + right + bottom);
        Gizmos.DrawLine(pos + left + top, pos + right + top);
        Gizmos.DrawLine(pos + left + bottom, pos + right + bottom);

        for (int i = 0; i < 4; ++i)
        {
            if (connections[i])
            {
                Vector2 multiplier = (new Vector2(widthInCells + 1, heightInCells + 1) * 0.5f * cellSize);
                Vector2 drawPos = ((Dir)i).ToV2().MultipliedBy(multiplier);
                Gizmos.DrawCube((Vector2)transform.position + drawPos, Vector3.one * cellSize * 0.95f);
            }
        }

        foreach (bool conn in connections)
        {

        }
    }
}
