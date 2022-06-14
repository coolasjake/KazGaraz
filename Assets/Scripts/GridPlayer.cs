using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridPlayer : MonoBehaviour
{
    public Animator animator;

    public void FixedMove()
    {
        Vector3 movement = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
            movement += new Vector3(0, Controller.gridScale);
        else if (Input.GetKey(KeyCode.S))
            movement += new Vector3(0, -Controller.gridScale);
        else if (Input.GetKey(KeyCode.D))
            movement += new Vector3(Controller.gridScale, 0);
        else if (Input.GetKey(KeyCode.A))
            movement += new Vector3(-Controller.gridScale, 0);

        if (!Physics2D.OverlapPoint(transform.position + movement))
            transform.position += movement;
    }

    public void Move(Dir dir)
    {
        transform.position += dir.ToV3() * Controller.gridScale;
        animator.SetInteger("Direction", (int)dir);
    }

    public void Idle()
    {
        animator.SetInteger("Direction", -1);
    }
}
