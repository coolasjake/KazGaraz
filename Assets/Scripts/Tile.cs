using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    public float connectionSize = 0.5f;
    public float width = 0;
    public List<ConnectPoint> connectionPoints = new List<ConnectPoint>();
    
    public bool HasConnection(Dir direction)
    {
        foreach (ConnectPoint cp in connectionPoints)
        {
            if (cp.direction == direction)
                return true;
        }
        return false;
    }

    public bool HasConnection(Dir direction, out Vector2 point)
    {
        foreach (ConnectPoint cp in connectionPoints)
        {
            if (cp.direction == direction)
            {
                point = (Vector2)transform.position + cp.relativePosition;
                return true;
            }
        }
        point = Vector2.zero;
        return false;
    }

    #region Gizmos
    private Vector3 d;
    private void OnDrawGizmosSelected()
    {
        d = Vector3.back * 0.001f;

        Gizmos.color = Color.cyan;
        //Draw left lines
        Vector3 left = transform.position + d + Vector3.left * width * 0.5f;
        Vector3 right = transform.position + d + Vector3.right * width * 0.5f;

        //Draw width lines
        Gizmos.DrawLine(left, right);
        Gizmos.DrawLine(right + Vector3.up, right + Vector3.down);
        Gizmos.DrawLine(left + Vector3.up, left + Vector3.down);

        Gizmos.color = Color.magenta;
        foreach (ConnectPoint cp in connectionPoints)
            DrawConnectionPoint(cp);
    }

    void DrawConnectionPoint(ConnectPoint cp)
    {
        Vector3 offset = Vector3.right * connectionSize * 0.5f;
        Vector3 height = Vector3.up * connectionSize * 0.5f;
        Vector3 conPoint = cp.relativePosition;
        //Draw exit (connection point)
        if (cp.direction == Dir.top || cp.direction == Dir.bottom)
        {
            Gizmos.DrawLine(transform.position + d + conPoint + height, transform.position + d + conPoint - height);
            if (cp.direction == Dir.bottom)
            {
                Gizmos.DrawLine(transform.position + d + conPoint - height, transform.position + d + conPoint + offset);
                Gizmos.DrawLine(transform.position + d + conPoint - height, transform.position + d + conPoint - offset);
            }
            else
            {
                Gizmos.DrawLine(transform.position + d + conPoint + height, transform.position + d + conPoint + offset);
                Gizmos.DrawLine(transform.position + d + conPoint + height, transform.position + d + conPoint - offset);
            }
        }
        else
        {
            Gizmos.DrawLine(transform.position + d + conPoint + offset, transform.position + d + conPoint - offset);
            if (cp.direction == Dir.left)
            {
                Gizmos.DrawLine(transform.position + d + conPoint - offset, transform.position + d + conPoint + height);
                Gizmos.DrawLine(transform.position + d + conPoint - offset, transform.position + d + conPoint - height);
            }
            else
            {
                Gizmos.DrawLine(transform.position + d + conPoint + offset, transform.position + d + conPoint + height);
                Gizmos.DrawLine(transform.position + d + conPoint + offset, transform.position + d + conPoint - height);
            }
        }
    }
    #endregion
}

[System.Serializable]
public class ConnectPoint
{
    public Dir direction = Dir.bottom;
    public Vector2 relativePosition = Vector2.zero;
    public bool isUsed = false;
}
