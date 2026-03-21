using UnityEngine;

public class Enemy : MonoBehaviour
{
    public Transform player; // ตัวแปรอ้างอิงถึงผู้เล่น
    public float speed = 3f; // ความเร็วของศัตรู

    void Update()
    {
        if (player != null)
        {
            // ให้ศัตรูเดินเข้าหาตำแหน่งของผู้เล่น
            transform.position = Vector3.MoveTowards(transform.position, player.position, speed * Time.deltaTime);
        }
    }
}