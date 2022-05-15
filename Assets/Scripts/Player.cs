using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    public float jumpVel = 5;
    public float acceleration = 1;
    public float maxHorSpeed = 2;
    public float maxVertSpeed = 10;
    public float noInputDeccel = 2;

    public LayerMask jumpCheckMask = new LayerMask();

    private Rigidbody2D _RB;

    // Start is called before the first frame update
    void Start()
    {
        _RB = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (Physics2D.OverlapBox(transform.position, transform.localScale, 0, jumpCheckMask.value))
            _RB.AddForce(new Vector2(0, jumpVel));
        }

        if ((Input.GetKey(KeyCode.A)) || (Input.GetKey(KeyCode.D)))
        {
            if (Input.GetKey(KeyCode.A))
            {
                _RB.AddForce(new Vector2(-acceleration, 0));
            }

            if (Input.GetKey(KeyCode.D))
            {
                _RB.AddForce(new Vector2(acceleration, 0));
            }
        }
        else
        {
            _RB.AddForce(new Vector2(acceleration, 0));
        }

        _RB.velocity = new Vector2(Mathf.Clamp(_RB.velocity.x, -maxHorSpeed, maxHorSpeed), Mathf.Clamp(_RB.velocity.y, -maxVertSpeed, maxVertSpeed));
    }
}
