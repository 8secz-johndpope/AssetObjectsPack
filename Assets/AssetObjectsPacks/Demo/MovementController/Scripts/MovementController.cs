﻿using System.Collections;
using UnityEngine;
using AssetObjectsPacks;

namespace Movement {

[RequireComponent(typeof(EventPlayer))]
public class MovementController : MonoBehaviour {
    
    //change if you've changed the pack name or the animatins pack itself
    public const string animationPackName = "Animations";

    public MovementBehavior behavior;
    public Movement.Direction direction;
    [Range(0,2)] public int speed;
    [Range(0,1)] public int stance;

    EventPlayer eventPlayer;
    public int eventLayer = 0;

    public Vector3 moveDireciton { get { return Movement.GetRelativeTransformDirection(direction, transform); } } 
            
    //int lastSpeed = -1, lastStance = -1;
    //Movement.Direction lastDirection = Movement.Direction.Backwards;
    const string speedName = "Speed", directionName = "Direction", stanceName = "Stance";


    void Awake () {     
        eventPlayer = GetComponent<EventPlayer>();  
        eventPlayer.AddParameters(
            new CustomParameter[] {
                // paremeters linked with script properties:
                new CustomParameter ( speedName, () => speed ),
                new CustomParameter ( directionName, () => (int)direction ),
                new CustomParameter ( stanceName, () => stance ),
            }
        );
    }
    void Update () {
        CheckSpeedDirectionChanges();
    }
    

    public bool overrideMovement { get { return overrideMove || eventPlayer.cueMoving; } }
    bool overrideMove;
    /*
        parameters:
            layer (internally set), override move

        overrides movement so no other movements can trigger
    */
    void OverrideMovement (object[] parameters) {
        overrideMove = (bool)parameters[1];    
        Debug.Log("override movment: " + overrideMove);
        
    }    

    /*
        parameters:
            layer (internally set)
    
        stops all movement and plays a loop animation for a single frame, 
        so whatever animaition plays next will exit into a "still" loop

        sets the player to stop the cue immediately after playing that frame
    */
    void StopMovement (object[] parameters) {
        //force change so change doesnt register and override cue animation
        
        //SetDirection(Movement.Direction.Forward);
        //SetSpeed(0);

        speed = 0;
        speedTracker.SetLastValue(speed);
        direction = Movement.Direction.Forward;
        directionTracker.SetLastValue(direction);
    }

    /*
        parameters:
            layer (internally set), int speed (optional), int direction (optional, current if not there)

        starts playing movement animation loop for single frame

        cue ends right after
    */
    void StartMovement(object[] parameters) {
        int l = parameters.Length;
        //unpack parameters
        int newSpeed = (l > 1) ? (int)parameters[1] : -1;
        //Movement.Direction newDirection = (Movement.Direction)((l > 2) ? ((int)parameters[2]) : (int)direction);
        
        //force change so change doesnt register and override cue animation
        speed = newSpeed <= 0 ? CalculateSpeed(newSpeed) : newSpeed;
        speedTracker.SetLastValue(speed);
        
        //SetSpeed(newSpeed <= 0 ? CalculateSpeed(newSpeed) : newSpeed);

        //SetDirection(newDirection);
    }

        
    
    public int CalculateSpeed (int newSpeed) {
        if (newSpeed <= 0) {
            //use current or walk
            return Mathf.Max(speed, 1);
        }
        return newSpeed;
    }

    public void UpdateLoopState () {


        //immediately play the loop unless we're jumping or overriding movement
        bool asInterruptor = !overrideMovement;
        // Debug.Log("upading loops // " + asInterruptor);

        Playlist.InitializePerformance("update Loop state", speed == 0 ? behavior.stillCue : behavior.moveCue, eventPlayer, false, eventLayer, new MiniTransform( Vector3.zero, Quaternion.identity), asInterruptor);
    }

    

    ValueTracker<int> stanceTracker = new ValueTracker<int>(-1), speedTracker = new ValueTracker<int>(-1);
    ValueTracker<Movement.Direction> directionTracker = new ValueTracker<Movement.Direction>(Movement.Direction.Forward);

    void CheckSpeedDirectionChanges() {
        if (speed == 0) {
            direction = Movement.Direction.Forward;
        }
        
        bool changedSpeed = speedTracker.CheckValueChange(speed);
        bool changedDirection = directionTracker.CheckValueChange(direction);
        bool changedStance = stanceTracker.CheckValueChange(stance);
        bool changed = changedSpeed || changedDirection || changedStance;
        if (changed) {
            UpdateLoopState();
        }
    }
    /*

    bool SetDirection(Movement.Direction direction) {
        bool changed = direction != lastDirection;
        this.direction = direction;
        lastDirection = direction;
        return changed;
    }
    bool SetSpeed(int speed) {
        bool changed = speed != lastSpeed;
        this.speed = speed;
        lastSpeed = speed;
        return changed;
    }
    bool SetStance(int stance) {
        bool changed = stance != lastStance;
        this.stance = stance;
        lastStance = stance;
        return changed;
    }
     */

}
}
