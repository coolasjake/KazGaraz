using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GridPlayer : MonoBehaviour
{
    public Animator animator;
    public LayerMask recordLayer;
    public GameObject recordPickup;
    public float collectionSize = 2f;

    public List<AudioClip> pickupSounds = new List<AudioClip>();
    public AudioSource audioPlayer;

    void Start()
    {
        if (audioPlayer == null)
            audioPlayer = GetComponent<AudioSource>();
    }

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

    public bool CheckForRecord()
    {
        Collider2D record = Physics2D.OverlapCircle(transform.position, Controller.gridScale * collectionSize, recordLayer);
        if (record)
        {
            Instantiate(recordPickup, record.transform.position, Quaternion.identity);
            Destroy(record.gameObject);
            if (pickupSounds.Count > 0)
            {
                audioPlayer.clip = pickupSounds[Random.Range(0, pickupSounds.Count)];
                audioPlayer.Play();
            }
            return true;
        }
        return false;
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
