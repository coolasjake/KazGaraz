using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public Dir[] path;

    public void Move(Vector3 playerPos)
    {
        if (path != null && path.Length > 0)
        {
            transform.position += (Vector3)path[0].ToV2() * Controller.gridScale;
        }
        else
        {
            Vector2 delta = playerPos - transform.position;
            Vector3 move = Vector2.zero;
            if (delta.sqrMagnitude > Mathf.Pow(Controller.gridScale * 3f, 2))
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

            transform.position += move * Controller.gridScale;
        }
    }
}
