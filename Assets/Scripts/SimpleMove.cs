using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleMove : MonoBehaviour
{
    // Update is called once per frame
    public Vector2 speed = new Vector2(0, 0);
    public Vector2 targetSpeed;
    public float accel = .2f;
    public float topSpeed = 5f;
    public Raycast raycaster;
    void Update()
    {
        float targetX = 0, targetY = 0;
        if (Input.GetKey("a")) targetX -= topSpeed;
        if (Input.GetKey("d")) targetX += topSpeed;
        if (Input.GetKey("w")) targetY += topSpeed;
        if (Input.GetKey("s")) targetY -= topSpeed;
        targetSpeed = new Vector2(targetX, targetY);
        var accelDelta = targetSpeed - speed;
        var accelMag = Mathf.Min(accelDelta.magnitude, accel * Time.deltaTime);
        speed += accelDelta.normalized * accelMag;
        this.transform.localPosition += new Vector3(speed.x, speed.y, 0) * Time.deltaTime;
        if (Input.GetMouseButton(1) || Input.GetMouseButtonDown(0)) {
            raycaster.RunRaycast(transform.position);
        }
    }
}
