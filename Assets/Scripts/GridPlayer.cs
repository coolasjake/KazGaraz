using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridPlayer : MonoBehaviour
{
    public float gridScale = 0.5f;

    private float[] heldTime = new float[4];
    private enum Dir
    {
        up,
        down,
        left,
        right
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 movement = Vector3.zero;
        if (Input.GetKeyDown(KeyCode.W))
            movement += new Vector3(0, gridScale);
        if (Input.GetKeyDown(KeyCode.S))
            movement += new Vector3(0, -gridScale);
        if (Input.GetKeyDown(KeyCode.D))
            movement += new Vector3(gridScale, 0);
        if (Input.GetKeyDown(KeyCode.A))
            movement += new Vector3(-gridScale, 0);

        if (!Physics2D.OverlapPoint(transform.position + movement))
            transform.position += movement;
    }
}
