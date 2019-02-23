﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using AssetObjectsPacks;

public class FaceMovementDirection : MonoBehaviour
{

    public float turnSpeed = 50;
    public float destinationThreshold = .1f;


    Transform destination;
    bool moving;
    EventPlayer player;    
    void Awake () {
        player = GetComponent<EventPlayer>();
    }

    System.Action endEvent;
    void InitializeMovementDirection_Event(Transform destination) {
        Debug.Log("overriding player");
        endEvent = player.OverrideEndEvent();
        Debug.Log("initializing");
        InitializeMovementDirection(destination);
    }
    void InitializeMovementDirection(Transform destination) {
        this.destination = destination;
        moving = true;
    }

    void FixedUpdate () {
        if (!moving) return;
        Vector3 dir = destination.position - transform.position;
        dir.y = 0;
        
        Quaternion lookRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * turnSpeed);
    }
    void Update () {
        if (!moving) return;

        Debug.DrawLine(destination.position, transform.position, Color.green);

        if (Vector3.Distance(transform.position, destination.position) <= destinationThreshold) {
            Debug.Log("arrived");
            destination = null;
            moving = false;

            if (endEvent != null) {
                endEvent();
                endEvent = null;
            }
        }
    }
}